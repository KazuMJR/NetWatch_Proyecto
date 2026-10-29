using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.JSInterop;
using NetWatch.Web.Components.Pages;
using NetWatch.Web.Models;
using NetWatch.Web.Services;

namespace NetWatch.Tests;

public sealed class DashboardLifecycleTests
{
    // Exercise the actual component lifecycle with delayed HTTP, without a real server or credentials.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NavigatingAwayDuringInitialLoadDoesNotRestartDisposedDashboard(bool delayReads)
    {
        using var handler = new DelayedApi(delayReads);
        var page = await CreatePage(handler);
        var loading = Call(page, "OnInitializedAsync");
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await page.DisposeAsync();
        await page.DisposeAsync(); // Repeated teardown must be harmless.
        Assert.True(handler.PendingToken.IsCancellationRequested);
        handler.Release.TrySetResult();
        await loading.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Null(Field(page, "data"));
        Assert.Null(Field(page, "error"));
        Assert.Null(Field(page, "lastRefreshUtc"));
        var count = handler.Requests;
        await Call(page, "RefreshNow");
        Assert.Equal(count, handler.Requests);
    }

    [Fact]
    public async Task ConcurrentRefreshIsIgnoredAndSuccessfulLoadStillUpdatesDashboard()
    {
        using var handler = new DelayedApi(false);
        var page = await CreatePage(handler);
        try
        {
            var loading = Call(page, "OnInitializedAsync");
            await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Call(page, "RefreshNow");
            Assert.Equal(1, handler.Requests);
            handler.Release.TrySetResult();
            await loading.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotNull(Field(page, "data"));
            Assert.NotNull(Field(page, "lastRefreshUtc"));
            Assert.Null(Field(page, "error"));
            Assert.Equal(4, handler.Requests);
            await Call(page, "RefreshNow");
            Assert.Equal(8, handler.Requests);
        }
        finally { await page.DisposeAsync(); }
    }

    private static async Task<Dashboard> CreatePage(DelayedApi handler)
    {
        var storage = new ProtectedSessionStorage(new MemoryStorageJs(), new EphemeralDataProtectionProvider());
        var auth = new ApiAuthenticationStateProvider(storage);
        await auth.SignInAsync(new LoginResponse("test-only-token", DateTime.UtcNow.AddHours(1), 1, "Prueba", "prueba", "Administrator"));
        var page = new Dashboard();
        typeof(Dashboard).GetProperty("Api", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(page, new NetWatchApiClient(new ClientFactory(handler), auth));
        return page;
    }

    private static Task Call(Dashboard page, string method) =>
        (Task)typeof(Dashboard).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(page, null)!;
    private static object? Field(Dashboard page, string field) =>
        typeof(Dashboard).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(page);

    private sealed class ClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("http://test.local/") };
    }

    private sealed class DelayedApi(bool delayReads) : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken PendingToken { get; private set; }
        public int Requests;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Requests);
            if ((request.Method == HttpMethod.Get) == delayReads)
            {
                PendingToken = cancellationToken;
                Started.TrySetResult();
                // Deliberately allow completion after disposal to reproduce the original race.
                await Release.Task;
            }
            var json = request.RequestUri!.AbsolutePath switch
            {
                "/api/dashboard" => "{\"total\":3,\"active\":3,\"recentAlerts\":[],\"recentMetrics\":[]}",
                _ => "[]"
            };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        }
    }

    private sealed class MemoryStorageJs : IJSRuntime
    {
        private readonly Dictionary<string, string> values = new();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            object? result = null;
            var key = (string)args![0]!;
            if (identifier.EndsWith("setItem")) values[key] = (string)args[1]!;
            if (identifier.EndsWith("getItem")) result = values.GetValueOrDefault(key);
            return ValueTask.FromResult(result is null ? default! : (TValue)result);
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
}

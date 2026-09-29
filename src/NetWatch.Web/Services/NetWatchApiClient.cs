using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace NetWatch.Web.Services;

public sealed class NetWatchApiClient(IHttpClientFactory factory, ApiAuthenticationStateProvider stateProvider)
{
    private async Task<HttpRequestMessage> RequestAsync(HttpMethod method, string uri, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        var token = await stateProvider.GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token)) throw new ApiException("Su sesión expiró. Inicie sesión nuevamente.");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    public async Task<T> GetAsync<T>(string uri)
    {
        using var request = await RequestAsync(HttpMethod.Get, uri);
        using var response = await factory.CreateClient("NetWatchAnonymous").SendAsync(request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<T>() ?? throw new ApiException("La API devolvió una respuesta vacía.");
    }

    public async Task<TResponse> PostAsync<TResponse>(string uri, object? body = null)
    {
        using var request = await RequestAsync(HttpMethod.Post, uri, body);
        using var response = await factory.CreateClient("NetWatchAnonymous").SendAsync(request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TResponse>() ?? throw new ApiException("La API devolvió una respuesta vacía.");
    }

    public async Task PostAsync(string uri, object? body = null)
    {
        using var request = await RequestAsync(HttpMethod.Post, uri, body);
        using var response = await factory.CreateClient("NetWatchAnonymous").SendAsync(request);
        await EnsureSuccessAsync(response);
    }

    public async Task<TResponse> PutAsync<TResponse>(string uri, object body)
    {
        using var request = await RequestAsync(HttpMethod.Put, uri, body);
        using var response = await factory.CreateClient("NetWatchAnonymous").SendAsync(request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TResponse>() ?? throw new ApiException("La API devolvió una respuesta vacía.");
    }

    public async Task PutAsync(string uri, object? body = null)
    {
        using var request = await RequestAsync(HttpMethod.Put, uri, body);
        using var response = await factory.CreateClient("NetWatchAnonymous").SendAsync(request);
        await EnsureSuccessAsync(response);
    }

    public async Task DeleteAsync(string uri)
    {
        using var request = await RequestAsync(HttpMethod.Delete, uri);
        using var response = await factory.CreateClient("NetWatchAnonymous").SendAsync(request);
        await EnsureSuccessAsync(response);
    }

    public async Task<(byte[] Data, string FileName)> DownloadAsync(string uri)
    {
        using var request = await RequestAsync(HttpMethod.Get, uri);
        using var response = await factory.CreateClient("NetWatchAnonymous").SendAsync(request);
        await EnsureSuccessAsync(response);
        var name = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? "report.csv";
        return (await response.Content.ReadAsByteArrayAsync(), name);
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) await stateProvider.SignOutAsync();
        if (!response.IsSuccessStatusCode) throw new ApiException(await ReadErrorAsync(response));
    }

    internal static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        try
        {
            var root = JsonDocument.Parse(text).RootElement;
            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                return error.GetString() ?? $"Error de API {(int)response.StatusCode}.";
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = errors.EnumerateObject()
                    .SelectMany(property => property.Value.EnumerateArray())
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())
                    .Where(message => !string.IsNullOrWhiteSpace(message))
                    .Distinct();
                var joined = string.Join(" ", messages!);
                if (!string.IsNullOrWhiteSpace(joined)) return joined;
            }
            return $"Error de API {(int)response.StatusCode}.";
        }
        catch { return string.IsNullOrWhiteSpace(text) ? $"Error de API {(int)response.StatusCode}." : text; }
    }
}

public sealed class ApiException(string message) : Exception(message);

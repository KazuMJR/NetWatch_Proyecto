using System.Globalization;
using Microsoft.AspNetCore.Components.Authorization;
using NetWatch.Web.Components;
using NetWatch.Web.Services;

var builder = WebApplication.CreateBuilder(args);
var spanishCulture = CultureInfo.GetCultureInfo("es-GT");
CultureInfo.DefaultThreadCurrentCulture = spanishCulture;
CultureInfo.DefaultThreadCurrentUICulture = spanishCulture;
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<ApiAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<ApiAuthenticationStateProvider>());
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<NetWatchApiClient>();
builder.Services.AddHttpClient("NetWatchAnonymous", client => client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5080/"));

var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.UseStaticFiles();
app.UseAntiforgery();
// The Razor host endpoint must remain anonymous. Page authorization is handled
// inside the Blazor circuit by AuthorizeRouteView and the API token provider.
// Without this override, ASP.NET Core tries to challenge the host request before
// the browser-backed authentication state is available and returns HTTP 500.
app.MapRazorComponents<App>().AddInteractiveServerRenderMode().AllowAnonymous();
app.Run();

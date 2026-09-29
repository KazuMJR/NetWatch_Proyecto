using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using NetWatch.Web.Models;

namespace NetWatch.Web.Services;

public sealed class ApiAuthenticationStateProvider(ProtectedSessionStorage storage) : AuthenticationStateProvider
{
    private const string UserKey = "netwatch.user";
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var stored = await storage.GetAsync<LoginResponse>(UserKey);
            if (!stored.Success || stored.Value is null || stored.Value.ExpiresAtUtc <= DateTime.UtcNow) return new AuthenticationState(Anonymous);
            return new AuthenticationState(CreatePrincipal(stored.Value));
        }
        catch { return new AuthenticationState(Anonymous); }
    }

    public async Task SignInAsync(LoginResponse response)
    {
        await storage.SetAsync(UserKey, response);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(CreatePrincipal(response))));
    }

    public async Task SignOutAsync()
    {
        await storage.DeleteAsync(UserKey);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            var stored = await storage.GetAsync<LoginResponse>(UserKey);
            return stored.Success && stored.Value?.ExpiresAtUtc > DateTime.UtcNow ? stored.Value.Token : null;
        }
        catch { return null; }
    }

    private static ClaimsPrincipal CreatePrincipal(LoginResponse user) => new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()), new Claim(ClaimTypes.Name, user.Username),
        new Claim(ClaimTypes.GivenName, user.FullName), new Claim(ClaimTypes.Role, user.Role)
    }, "NetWatch"));
}


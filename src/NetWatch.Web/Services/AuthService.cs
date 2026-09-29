using System.Net.Http.Json;
using NetWatch.Web.Models;

namespace NetWatch.Web.Services;

public sealed class AuthService(IHttpClientFactory factory, ApiAuthenticationStateProvider stateProvider)
{
    public async Task LoginAsync(LoginModel model)
    {
        var response = await factory.CreateClient("NetWatchAnonymous").PostAsJsonAsync("api/auth/login", model);
        if (!response.IsSuccessStatusCode) throw new ApiException(await NetWatchApiClient.ReadErrorAsync(response));
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>() ?? throw new ApiException("La API devolvió una respuesta de inicio de sesión no válida.");
        await stateProvider.SignInAsync(login);
    }
    public Task LogoutAsync() => stateProvider.SignOutAsync();
}

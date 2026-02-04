using Microsoft.AspNetCore.Components;

namespace BloodPressure.Web.Services;

public sealed class AuthService(
    AuthApiClient authApiClient,
    JwtAuthenticationStateProvider authStateProvider,
    NavigationManager navigationManager)
{
    public async Task StartLoginAsync(CancellationToken cancellationToken)
    {
        var loginUrl = await authApiClient.GetLoginUrlAsync(cancellationToken);
        navigationManager.NavigateTo(loginUrl.Url, forceLoad: true);
    }

    public async Task CompleteLoginAsync(string token, DateTimeOffset expiresAtUtc)
    {
        await authStateProvider.SignInAsync(token, expiresAtUtc);
        navigationManager.NavigateTo("/readings");
    }

    public async Task LogoutAsync()
    {
        await authStateProvider.SignOutAsync();
        navigationManager.NavigateTo("/login");
    }
}

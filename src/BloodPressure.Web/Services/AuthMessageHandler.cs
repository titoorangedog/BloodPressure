using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;

namespace BloodPressure.Web.Services;

public sealed class AuthMessageHandler : DelegatingHandler
{
    private readonly TokenStorage tokenStorage;
    private readonly JwtAuthenticationStateProvider authStateProvider;
    private readonly NavigationManager navigationManager;

    public AuthMessageHandler(
        TokenStorage tokenStorage,
        JwtAuthenticationStateProvider authStateProvider,
        NavigationManager navigationManager)
    {
        this.tokenStorage = tokenStorage;
        this.authStateProvider = authStateProvider;
        this.navigationManager = navigationManager;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var (token, expiresAtUtc) = await tokenStorage.GetAsync();
        if (!string.IsNullOrWhiteSpace(token) && expiresAtUtc is not null && expiresAtUtc > DateTimeOffset.UtcNow)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await authStateProvider.SignOutAsync();
            navigationManager.NavigateTo("/login?reason=expired", forceLoad: true);
        }

        return response;
    }
}

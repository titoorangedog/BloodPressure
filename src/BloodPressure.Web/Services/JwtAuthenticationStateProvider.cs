using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace BloodPressure.Web.Services;

public sealed class JwtAuthenticationStateProvider(TokenStorage tokenStorage)
    : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var (token, expiresAtUtc) = await tokenStorage.GetAsync();
        if (string.IsNullOrWhiteSpace(token) || expiresAtUtc is null || expiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return new AuthenticationState(Anonymous);
        }

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var identity = new ClaimsIdentity(jwt.Claims, "jwt");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public async Task SignInAsync(string token, DateTimeOffset expiresAtUtc)
    {
        await tokenStorage.StoreAsync(token, expiresAtUtc);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task SignOutAsync()
    {
        await tokenStorage.ClearAsync();
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));
    }
}

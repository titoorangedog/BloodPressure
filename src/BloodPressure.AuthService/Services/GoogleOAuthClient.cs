using System.Net.Http.Json;
using System.Text.Json.Serialization;
using BloodPressure.Shared.Auth;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace BloodPressure.AuthService.Services;

public sealed class GoogleOAuthClient(HttpClient httpClient, IOptions<GoogleOAuthOptions> options)
{
    private readonly GoogleOAuthOptions _options = options.Value;

    public string BuildLoginUrl(string state)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.CallbackUrl,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["state"] = state
        };

        var queryString = string.Join("&", query.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value ?? string.Empty)}"));
        return $"https://accounts.google.com/o/oauth2/v2/auth?{queryString}";
    }

    public async Task<GoogleJsonWebSignature.Payload> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        var tokenResponse = await httpClient.PostAsJsonAsync(
            "https://oauth2.googleapis.com/token",
            new
            {
                client_id = _options.ClientId,
                client_secret = _options.ClientSecret,
                redirect_uri = _options.CallbackUrl,
                grant_type = "authorization_code",
                code
            },
            cancellationToken);

        tokenResponse.EnsureSuccessStatusCode();

        var payload = await tokenResponse.Content.ReadFromJsonAsync<TokenExchangeResponse>(cancellationToken: cancellationToken);
        if (payload?.IdToken is null)
        {
            throw new InvalidOperationException("Missing id_token in Google response.");
        }

        return await GoogleJsonWebSignature.ValidateAsync(payload.IdToken);
    }

    private sealed record TokenExchangeResponse
    {
        [JsonPropertyName("id_token")]
        public string? IdToken { get; init; }
    }
}

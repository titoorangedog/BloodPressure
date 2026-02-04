using Microsoft.JSInterop;

namespace BloodPressure.Web.Services;

public sealed class TokenStorage(IJSRuntime jsRuntime)
{
    private const string TokenKey = "bp_token";
    private const string ExpiresKey = "bp_token_expires";

    public async ValueTask StoreAsync(string token, DateTimeOffset expiresAtUtc)
    {
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, token);
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", ExpiresKey, expiresAtUtc.ToString("O"));
    }

    public async ValueTask<(string? Token, DateTimeOffset? ExpiresAtUtc)> GetAsync()
    {
        var token = await jsRuntime.InvokeAsync<string>("localStorage.getItem", TokenKey);
        var expiresRaw = await jsRuntime.InvokeAsync<string>("localStorage.getItem", ExpiresKey);
        if (DateTimeOffset.TryParse(expiresRaw, out var expires))
        {
            return (token, expires);
        }

        return (token, null);
    }

    public async ValueTask ClearAsync()
    {
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", ExpiresKey);
    }
}

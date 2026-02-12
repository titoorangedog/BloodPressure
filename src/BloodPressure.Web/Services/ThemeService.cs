using Microsoft.JSInterop;

namespace BloodPressure.Web.Services;

public sealed class ThemeService(IJSRuntime jsRuntime)
{
    private const string DefaultTheme = "light";
    private const string StorageKey = "bp.theme";
    private static readonly HashSet<string> SupportedThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "light",
        "dark"
    };

    private bool _isInitialized;

    public event Action? ThemeChanged;

    public string CurrentTheme { get; private set; } = DefaultTheme;

    public bool IsDarkTheme => string.Equals(CurrentTheme, "dark", StringComparison.OrdinalIgnoreCase);

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        var storedTheme = await GetStoredThemeAsync();
        var theme = NormalizeTheme(storedTheme) ?? await GetSystemThemeAsync() ?? DefaultTheme;
        await ApplyThemeAsync(theme, persist: false);
        _isInitialized = true;
    }

    public async Task ToggleThemeAsync()
    {
        var nextTheme = IsDarkTheme ? "light" : "dark";
        await SetThemeAsync(nextTheme);
    }

    public async Task SetThemeAsync(string theme)
    {
        var normalized = NormalizeTheme(theme);
        if (normalized is null || string.Equals(normalized, CurrentTheme, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await ApplyThemeAsync(normalized, persist: true);
        ThemeChanged?.Invoke();
    }

    private async Task ApplyThemeAsync(string theme, bool persist)
    {
        CurrentTheme = theme;
        await jsRuntime.InvokeVoidAsync("bpTheme.applyTheme", theme);
        if (persist)
        {
            await jsRuntime.InvokeVoidAsync("bpTheme.setTheme", StorageKey, theme);
        }
    }

    private async Task<string?> GetStoredThemeAsync()
    {
        try
        {
            return await jsRuntime.InvokeAsync<string?>("bpTheme.getTheme", StorageKey);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> GetSystemThemeAsync()
    {
        try
        {
            return NormalizeTheme(await jsRuntime.InvokeAsync<string?>("bpTheme.getSystemTheme"));
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeTheme(string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
        {
            return null;
        }

        var normalized = theme.Trim().ToLowerInvariant();
        return SupportedThemes.Contains(normalized) ? normalized : null;
    }
}

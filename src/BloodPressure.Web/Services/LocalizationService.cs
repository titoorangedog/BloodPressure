using System.Globalization;
using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace BloodPressure.Web.Services;

public sealed class LocalizationService(HttpClient httpClient, IJSRuntime jsRuntime)
{
    private const string DefaultLanguageCode = "it";
    private const string StorageKey = "bp.language";
    private static readonly string TranslationCacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
    private Dictionary<string, string> _translations = new(StringComparer.OrdinalIgnoreCase);
    private bool _isInitialized;

    public event Action? LanguageChanged;

    public string CurrentLanguageCode { get; private set; } = DefaultLanguageCode;

    public IReadOnlyList<LanguageOption> SupportedLanguages { get; } =
    [
        new("it", "Italiano", "IT", "it"),
        new("en", "English", "GB", "en"),
        new("es", "Espanol", "ES", "es"),
        new("de", "Deutsch", "DE", "de"),
        new("fr", "Francais", "FR", "fr"),
        new("zh", "中文", "CN", "zh")
    ];

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        var stored = await GetStoredLanguageAsync();
        var language = string.IsNullOrWhiteSpace(stored) ? DefaultLanguageCode : stored;
        if (!IsSupported(language))
        {
            language = DefaultLanguageCode;
        }

        await LoadLanguageInternalAsync(language);
        _isInitialized = true;
    }

    public async Task SetLanguageAsync(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode) || !IsSupported(languageCode))
        {
            return;
        }

        if (string.Equals(CurrentLanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await LoadLanguageInternalAsync(languageCode);
        await jsRuntime.InvokeVoidAsync("bpLocalization.setLanguage", StorageKey, languageCode);
        LanguageChanged?.Invoke();
    }

    public string T(string key, string? fallback = null)
    {
        if (_translations.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return fallback ?? key;
    }

    public string TFormat(string key, string fallbackTemplate, params object[] args)
    {
        var template = T(key, fallbackTemplate);
        return string.Format(template, args);
    }

    private async Task LoadLanguageInternalAsync(string languageCode)
    {
        var data = await httpClient.GetFromJsonAsync<Dictionary<string, string>>($"i18n/{languageCode}.json?v={TranslationCacheBuster}");
        _translations = data ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        CurrentLanguageCode = languageCode;
        await jsRuntime.InvokeVoidAsync("bpLocalization.applyDocumentLanguage", languageCode);
    }

    private async Task<string?> GetStoredLanguageAsync()
    {
        try
        {
            return await jsRuntime.InvokeAsync<string?>("bpLocalization.getLanguage", StorageKey);
        }
        catch
        {
            return null;
        }
    }

    private bool IsSupported(string languageCode)
    {
        return SupportedLanguages.Any(x => string.Equals(x.Code, languageCode, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record LanguageOption(string Code, string DisplayName, string FlagCode, string HtmlLangCode);

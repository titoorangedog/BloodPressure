namespace BloodPressure.Mobile;

public partial class MainPage : ContentPage
{
    private const string WebUrlPreferenceKey = "bp.web.url";
    private const string DefaultWebUrl = "http://10.0.2.2:5172";

    public string CurrentUrl { get; private set; } = string.Empty;

    public MainPage()
    {
        InitializeComponent();
        BindingContext = this;

        CurrentUrl = BuildWebUrl();
        OnPropertyChanged(nameof(CurrentUrl));
        LoadWebApp();
    }

    private static string BuildWebUrl()
    {
        var configured = Preferences.Default.Get(WebUrlPreferenceKey, string.Empty)?.Trim();
        return string.IsNullOrWhiteSpace(configured) ? DefaultWebUrl : configured;
    }

    private void LoadWebApp()
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            OfflineOverlay.IsVisible = true;
            BusyOverlay.IsVisible = false;
            return;
        }

        OfflineOverlay.IsVisible = false;
        BusyOverlay.IsVisible = true;
        AppWebView.Source = CurrentUrl;
    }

    private void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        BusyOverlay.IsVisible = true;
        OfflineOverlay.IsVisible = false;
    }

    private void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        BusyOverlay.IsVisible = false;
        OfflineOverlay.IsVisible = e.Result != WebNavigationResult.Success;
    }

    private void OnReloadClicked(object? sender, EventArgs e)
    {
        LoadWebApp();
    }

    private async void OnOpenInBrowserClicked(object? sender, EventArgs e)
    {
        if (!Uri.TryCreate(CurrentUrl, UriKind.Absolute, out var uri))
        {
            await DisplayAlertAsync("URL non valida", "Configura un URL valido per la Web app.", "OK");
            return;
        }

        await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
    }

    private void OnRetryClicked(object? sender, EventArgs e)
    {
        LoadWebApp();
    }

    private async void OnChangeUrlClicked(object? sender, EventArgs e)
    {
        var value = await DisplayPromptAsync(
            "URL Web App",
            "Inserisci l'URL base della Web app BloodPressure.",
            initialValue: CurrentUrl,
            placeholder: "http://10.0.2.2:5172");

        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        value = value.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            await DisplayAlertAsync("URL non valida", "Usa un URL HTTP/HTTPS valido.", "OK");
            return;
        }

        CurrentUrl = value;
        Preferences.Default.Set(WebUrlPreferenceKey, value);
        OnPropertyChanged(nameof(CurrentUrl));
        LoadWebApp();
    }
}


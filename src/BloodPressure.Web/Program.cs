using BloodPressure.Web;
using BloodPressure.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<TokenStorage>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddTransient<AuthMessageHandler>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<ToastService>();
builder.Services.AddScoped<LocalizationService>();

string ResolveApiBase(string key)
{
    var value = builder.Configuration[key];
    var baseAddress = builder.HostEnvironment.BaseAddress;
    if (string.IsNullOrWhiteSpace(value) ||
        string.Equals(value.TrimEnd('/'), baseAddress.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
    {
        value = key switch
        {
            "Api:AuthService" => "/api/auth/",
            "Api:WriteService" => "/api/write/",
            "Api:ReadService" => "/api/read/",
            "Api:StatsService" => "/api/stats/",
            _ => builder.HostEnvironment.BaseAddress
        };
    }

    if (value.StartsWith("/"))
    {
        return new Uri(new Uri(builder.HostEnvironment.BaseAddress), value).ToString();
    }

    return value;
}

builder.Services.AddHttpClient<AuthApiClient>(client =>
{
    client.BaseAddress = new Uri(ResolveApiBase("Api:AuthService"));
});

builder.Services.AddHttpClient<ReadApiClient>(client =>
{
    client.BaseAddress = new Uri(ResolveApiBase("Api:ReadService"));
}).AddHttpMessageHandler<AuthMessageHandler>();

builder.Services.AddHttpClient<WriteApiClient>(client =>
{
    client.BaseAddress = new Uri(ResolveApiBase("Api:WriteService"));
}).AddHttpMessageHandler<AuthMessageHandler>();

builder.Services.AddHttpClient<StatsApiClient>(client =>
{
    client.BaseAddress = new Uri(ResolveApiBase("Api:StatsService"));
}).AddHttpMessageHandler<AuthMessageHandler>();

await builder.Build().RunAsync();

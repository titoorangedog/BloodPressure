using BloodPressure.Web;
using BloodPressure.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddAuthorizationCore();

builder.Services.AddScoped<TokenStorage>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddTransient<AuthMessageHandler>();
builder.Services.AddScoped<AuthService>();

string ResolveApiBase(string key) => builder.Configuration[key] ?? builder.HostEnvironment.BaseAddress;

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

using HouseholdFinances.Frontend.Components;
using HouseholdFinances.Frontend.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor component services (theming, dialogs, snackbars, etc.).
builder.Services.AddMudServices();

// Shared API client layer. The base URL is configuration-driven so no environment is hard-coded;
// the Blazor Server host calls the API server-to-server, so the bearer token never reaches the
// browser and no CORS handling is required.
var apiBaseUrl = builder.Configuration["Api:BaseUrl"];
if (string.IsNullOrWhiteSpace(apiBaseUrl))
{
    throw new InvalidOperationException(
        "The API base URL is required. Set the 'Api:BaseUrl' configuration value.");
}

if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiBaseUri) ||
    (apiBaseUri.Scheme != Uri.UriSchemeHttp && apiBaseUri.Scheme != Uri.UriSchemeHttps))
{
    throw new InvalidOperationException(
        $"The 'Api:BaseUrl' configuration value must be an absolute http or https URL, but was '{apiBaseUrl}'.");
}

builder.Services.AddScoped<ITokenStore, InMemoryTokenStore>();
builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
{
    client.BaseAddress = apiBaseUri;
});

// Frontend-only client services.
builder.Services.AddScoped<ThemeService>();

// Authentication entry point for the Login page. The Google ID token is obtained in the browser
// through Google Identity Services (the interop boundary), and the Blazor Server host exchanges it
// for the API-issued pair server to server, so the tokens never reach the browser. The Google
// client id is configuration (Authentication:Google:ClientId), not a secret.
builder.Services.AddScoped<IGoogleIdentityProvider, GoogleIdentityInterop>();
builder.Services.AddScoped<IAuthenticationService, GoogleAuthenticationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

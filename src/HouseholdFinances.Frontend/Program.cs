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

// Authentication entry point for the Login page. The real Google Identity sign-in, backed by the
// API-issued JWT, is delivered by backend issue #11; until then this honest placeholder reports
// sign-in as unavailable instead of pretending to authenticate.
builder.Services.AddScoped<IAuthenticationService, UnavailableAuthenticationService>();

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

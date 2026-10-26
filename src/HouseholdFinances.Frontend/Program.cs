using HouseholdFinances.Frontend.Components;
using HouseholdFinances.Frontend.Services;
using Microsoft.AspNetCore.DataProtection;
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

builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
{
    client.BaseAddress = apiBaseUri;
});

// Persistent session (issue #28). The browser holds only an opaque session id in an HTTP-only
// cookie; the token pair stays server-side, encrypted with ASP.NET Core Data Protection under a
// key ring that must itself be persistent for a restart to honour a session. Both directories are
// configuration-driven and default outside the repository (see README).
var sessionOptions = PersistentSessionOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(sessionOptions);

builder.Services.AddDataProtection()
    .SetApplicationName("HouseholdFinances.Frontend")
    .PersistKeysToFileSystem(new DirectoryInfo(sessionOptions.KeyRingDirectory));

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ActiveSessionRegistry>();
builder.Services.AddSingleton<FileSessionTokenRepository>();
builder.Services.AddSingleton<ISessionTokenRepository>(
    services => services.GetRequiredService<FileSessionTokenRepository>());
builder.Services.AddScoped<ISessionIdAccessor, SessionIdAccessor>();
builder.Services.AddScoped<ITokenStore, PersistentTokenStore>();

// Frontend-only client services.
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<PeriodSelectionService>();

// The only time source the pages use, so "now" is explicit and pinnable in tests. Its UTC clock
// matches the assumption that windows are calendar dates in UTC.
builder.Services.AddSingleton(TimeProvider.System);

// Authentication entry point for the Login page. The Google ID token is obtained in the browser
// through Google Identity Services (the interop boundary), and the Blazor Server host exchanges it
// for the API-issued pair server to server, so the tokens never reach the browser. The Google
// client id is configuration (Authentication:Google:ClientId), not a secret.
builder.Services.AddScoped<IGoogleIdentityProvider, GoogleIdentityInterop>();
builder.Services.AddScoped<IAuthenticationService, GoogleAuthenticationService>();

var app = builder.Build();

// Fail fast when the session locations are unusable, and sweep records no browser can still use.
Directory.CreateDirectory(sessionOptions.Directory);
Directory.CreateDirectory(sessionOptions.KeyRingDirectory);
var prunedSessionRecords = await app.Services
    .GetRequiredService<FileSessionTokenRepository>()
    .PruneExpiredRecordsAsync();
app.Logger.LogInformation(
    "Sessions are persisted in {SessionDirectory} with Data Protection keys in {KeyRingDirectory}; {PrunedRecordCount} expired record(s) removed.",
    sessionOptions.Directory,
    sessionOptions.KeyRingDirectory,
    prunedSessionRecords);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Issues and reads the HTTP-only session cookie on document requests (issue #28). A Blazor Server
// circuit cannot read or write cookies, so the cookie and the circuit's session identifier are
// bridged through this middleware and SessionStateBridge.
app.UseMiddleware<SessionCookieMiddleware>();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

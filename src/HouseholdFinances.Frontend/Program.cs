using HouseholdFinances.Frontend.Components;
using HouseholdFinances.Frontend.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MudBlazor component services (theming, dialogs, snackbars, etc.).
builder.Services.AddMudServices();

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

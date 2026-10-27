using Bunit;
using HouseholdFinances.Frontend.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using MainLayout = HouseholdFinances.Frontend.Components.Layout.MainLayout;

namespace HouseholdFinances.Frontend.Tests.Components;

/// <summary>
/// bUnit component tests for the shared layout (issue #43). MudBlazor renders the options of every
/// popover-based control - MudSelect, MudMenu, MudAutocomplete, MudDatePicker - through
/// MudPopoverProvider, so the layout has to register it.
/// </summary>
/// <remarks>
/// The Income and Bills interval selector is a MudSelect. While the layout omitted
/// MudPopoverProvider the dropdown opened with no selectable options, so an interval could not be
/// set on either page even though the page binding and the request payload were already correct.
/// These tests guard the provider registration that fixes it. They do not prove a browser renders
/// the layout, only that the component tree registers the provider the popover controls require.
/// </remarks>
public class MainLayoutTests : BunitContext, IAsyncLifetime
{
    public MainLayoutTests()
    {
        // MudBlazor components may touch JS interop; loose mode keeps the render side-effect free.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddScoped<ThemeService>();
        Services.AddSingleton<IAuthenticationService>(new StubAuthenticationService());
        Services.AddSingleton<IApiClient>(new StubApiClient());
        Services.AddScoped<HouseholdContextService>();
        Services.AddScoped<PageAccessService>();
    }

    // The dialog and snackbar providers resolve services that only implement IAsyncDisposable, so
    // the context must be disposed asynchronously.
    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await ((IAsyncDisposable)this).DisposeAsync();

    [Fact]
    public void MainLayout_RegistersTheMudPopoverProviderThatPopoverControlsNeed()
    {
        var cut = Render<MainLayout>();

        // Without this provider every popover-based control renders an empty overlay, which is what
        // stopped a recurrence interval from being selected on the Income and Bills pages.
        Assert.NotEmpty(cut.FindComponents<MudPopoverProvider>());
    }
}

using Bunit;
using HouseholdFinances.Frontend.Components.Layout;
using HouseholdFinances.Frontend.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace HouseholdFinances.Frontend.Tests.Components;

/// <summary>
/// bUnit component tests for the shared MainLayout shell: the green theme is applied through
/// MudThemeProvider and the primary navigation renders consistently for every page. The tests prove
/// the rendered shell structure; they do not prove the responsive breakpoints, theming in a real
/// browser, or keyboard focus behavior.
/// </summary>
public class MainLayoutShellTests : BunitContext, IAsyncLifetime
{
    private readonly ThemeService _themeService = new();

    public MainLayoutShellTests()
    {
        // MudBlazor components may touch JS interop; loose mode keeps render side-effect free.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(_themeService);
    }

    // Rendering the shell resolves MudBlazor services that only implement IAsyncDisposable, so the
    // context must be disposed asynchronously. xUnit routes teardown through IAsyncLifetime instead
    // of the synchronous IDisposable.Dispose when it is implemented.
    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await ((IAsyncDisposable)this).DisposeAsync();

    [Fact]
    public void MainLayout_AppliesTheGreenThemeFromThemeService()
    {
        var cut = Render<MainLayout>();

        var provider = cut.FindComponent<MudThemeProvider>();

        Assert.Same(_themeService.Theme, provider.Instance.Theme);
        Assert.NotEqual(
            _themeService.Theme.PaletteLight.Primary,
            _themeService.Theme.PaletteDark.Primary);
    }

    [Fact]
    public void MainLayout_RendersTheSharedNavigationForEveryPage()
    {
        var cut = Render<MainLayout>();

        Assert.Contains("Household Finances", cut.Markup);

        foreach (var label in new[]
                 {
                     "Home", "Login", "Setup", "Household Dashboard",
                     "Income", "Bills", "Profile", "Reset Password"
                 })
        {
            Assert.Contains(label, cut.Markup);
        }
    }
}

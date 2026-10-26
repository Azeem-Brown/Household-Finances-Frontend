using Bunit;
using Bunit.TestDoubles;
using HouseholdFinances.Frontend.Components.Layout;
using HouseholdFinances.Frontend.Services;
using Microsoft.AspNetCore.Components;
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
    private readonly StubAuthenticationService _authenticationService = new();

    public MainLayoutShellTests()
    {
        // MudBlazor components may touch JS interop; loose mode keeps render side-effect free.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(_themeService);
        Services.AddSingleton<IAuthenticationService>(_authenticationService);
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

    [Fact]
    public void MainLayout_HidesSignOutWhenThereIsNoSession()
    {
        _authenticationService.HasSession = false;

        var cut = Render<MainLayout>();

        Assert.Empty(cut.FindAll("[data-testid='signout']"));
    }

    [Fact]
    public void MainLayout_ShowsSignOutWhenSignedIn()
    {
        _authenticationService.HasSession = true;

        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='signout']")));
        Assert.Equal("Sign out", cut.Find("[data-testid='signout']").GetAttribute("aria-label"));
    }

    [Fact]
    public void MainLayout_SignOut_ClearsTheSessionAndReturnsToLogin()
    {
        _authenticationService.HasSession = true;
        var navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() => cut.Find("[data-testid='signout']"));
        cut.Find("[data-testid='signout']").Click();

        cut.WaitForAssertion(() => Assert.EndsWith("/login", navigation.Uri, StringComparison.Ordinal));
        Assert.Equal(1, _authenticationService.SignOutCallCount);
        Assert.Empty(cut.FindAll("[data-testid='signout']"));
    }
}

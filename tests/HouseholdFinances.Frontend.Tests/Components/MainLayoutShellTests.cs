using Bunit;
using Bunit.TestDoubles;
using HouseholdFinances.Frontend.Components.Layout;
using HouseholdFinances.Frontend.Models;
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
    private readonly StubApiClient _apiClient = new();
    private readonly BunitNavigationManager _navigation;

    public MainLayoutShellTests()
    {
        // MudBlazor components may touch JS interop; loose mode keeps render side-effect free.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton(_themeService);
        Services.AddSingleton<IAuthenticationService>(_authenticationService);
        Services.AddSingleton<IApiClient>(_apiClient);
        Services.AddScoped<HouseholdContextService>();
        Services.AddScoped<PageAccessService>();

        _navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
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
    public void MainLayout_SignedOut_OffersOnlyLogin()
    {
        _authenticationService.HasSession = false;

        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() => Assert.Contains("Login", cut.Markup));
        Assert.DoesNotContain("Setup", cut.Markup);
        Assert.DoesNotContain("Household Dashboard", cut.Markup);
        Assert.DoesNotContain("Income", cut.Markup);
        Assert.DoesNotContain("Bills", cut.Markup);
    }

    [Fact]
    public void MainLayout_SignedInWithoutHousehold_OffersSetupAndProfileOnly()
    {
        _authenticationService.HasSession = true;
        _apiClient.GetResponse = new List<Household>();

        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() => Assert.Contains("Setup", cut.Markup));
        Assert.Contains("Profile", cut.Markup);
        Assert.DoesNotContain("Login", cut.Markup);
        Assert.DoesNotContain("Household Dashboard", cut.Markup);
        Assert.DoesNotContain("Switch household", cut.Markup);
    }

    [Fact]
    public void MainLayout_SignedInWithHousehold_OffersEverythingExceptLoginAndSetup()
    {
        _authenticationService.HasSession = true;
        _apiClient.GetResponse = new List<Household>
        {
            new Household { Id = Guid.NewGuid(), Name = "Family" }
        };

        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() => Assert.Contains("Household Dashboard", cut.Markup));

        foreach (string label in new[]
                 {
                     "Home", "Household Dashboard", "Switch household", "Income",
                     "Bills", "Profile", "Household Branding", "Reset Password"
                 })
        {
            Assert.Contains(label, cut.Markup);
        }

        // Setup and Login are gone once the user can already work in a household.
        Assert.DoesNotContain("Setup", cut.Markup);
        Assert.DoesNotContain("Login", cut.Markup);
    }

    [Fact]
    public void MainLayout_SignedOutAtAProtectedPage_LandsOnLogin()
    {
        _authenticationService.HasSession = false;
        _navigation.NavigateTo("/dashboard");

        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() =>
            Assert.EndsWith("/login", _navigation.Uri, StringComparison.Ordinal));
    }

    [Fact]
    public void MainLayout_WithoutHouseholdAtAProtectedPage_LandsOnSetup()
    {
        _authenticationService.HasSession = true;
        _apiClient.GetResponse = new List<Household>();
        _navigation.NavigateTo("/households");

        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() =>
            Assert.EndsWith("/setup", _navigation.Uri, StringComparison.Ordinal));
    }

    [Fact]
    public void MainLayout_WithHouseholdAtSetup_LandsOnTheHouseholdList()
    {
        _authenticationService.HasSession = true;
        _apiClient.GetResponse = new List<Household>
        {
            new Household { Id = Guid.NewGuid(), Name = "Family" }
        };
        _navigation.NavigateTo("/setup");

        var cut = Render<MainLayout>(parameters => parameters.Add(
            layout => layout.Body,
            (RenderFragment)(builder => builder.AddMarkupContent(0, "<p data-testid='page-body'>page</p>"))));

        cut.WaitForAssertion(() =>
            Assert.EndsWith("/households", _navigation.Uri, StringComparison.Ordinal));

        // Setup is no longer offered once the user works in a household, and the app bar controls
        // keep working through the redirect.
        Assert.DoesNotContain("Setup", cut.Markup);
        Assert.NotEmpty(cut.FindAll("[data-testid='signout']"));
    }

    [Fact]
    public void MainLayout_AllowedPage_RendersThePageBody()
    {
        _authenticationService.HasSession = true;
        _apiClient.GetResponse = new List<Household>
        {
            new Household { Id = Guid.NewGuid(), Name = "Family" }
        };
        _navigation.NavigateTo("/dashboard");

        var cut = Render<MainLayout>(parameters => parameters.Add(
            layout => layout.Body,
            (RenderFragment)(builder => builder.AddMarkupContent(0, "<p data-testid='page-body'>page</p>"))));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='page-body']")));
        Assert.EndsWith("/dashboard", _navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayout_HouseholdLoadFailure_KeepsTheRequestedPage()
    {
        _authenticationService.HasSession = true;
        _apiClient.Throw = new ClientErrorException(ClientErrorCode.Unknown, "households");
        _navigation.NavigateTo("/households");

        var cut = Render<MainLayout>(parameters => parameters.Add(
            layout => layout.Body,
            (RenderFragment)(builder => builder.AddMarkupContent(0, "<p data-testid='page-body'>page</p>"))));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid='page-body']")));

        // A list that cannot be read is not "no household": the user is not sent to Setup.
        Assert.EndsWith("/households", _navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MainLayout_HouseholdCreatedWithoutReload_UpdatesTheMenu()
    {
        _authenticationService.HasSession = true;
        _apiClient.GetResponse = new List<Household>();

        var cut = Render<MainLayout>();

        cut.WaitForAssertion(() => Assert.Contains("Setup", cut.Markup));

        // Stand in for a page refreshing the household list after creating one.
        _apiClient.GetResponse = new List<Household>
        {
            new Household { Id = Guid.NewGuid(), Name = "Family" }
        };
        await Services.GetRequiredService<HouseholdContextService>().RefreshAsync();

        cut.WaitForAssertion(() => Assert.Contains("Household Dashboard", cut.Markup));
        Assert.DoesNotContain("Setup", cut.Markup);
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

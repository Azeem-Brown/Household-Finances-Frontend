using Bunit;
using Bunit.TestDoubles;
using HouseholdFinances.Frontend.Components.Pages;
using HouseholdFinances.Frontend.Models;
using HouseholdFinances.Frontend.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace HouseholdFinances.Frontend.Tests.Components;

/// <summary>
/// bUnit component tests for the household list that sits between sign-in and the dashboard: it
/// lists every household the user belongs to with its logo, marks the current choice, and picking
/// one selects it and opens the dashboard.
/// </summary>
/// <remarks>
/// The API client is replaced with a test double, so no live backend or browser is involved. The
/// tests prove the page requests the household list, renders each entry with its logo or a
/// placeholder, marks the current household, routes a pick through the shared selection, and shows
/// the empty and error states; they do not prove a browser renders the page or that the real API
/// returns the same shapes.
/// </remarks>
public class HouseholdsPageTests : BunitContext
{
    private static readonly Guid FirstId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly StubApiClient _apiClient = new();
    private readonly BunitNavigationManager _navigation;

    public HouseholdsPageTests()
    {
        // MudBlazor components may touch JS interop; loose mode keeps render side-effect free.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IApiClient>(_apiClient);
        Services.AddScoped<HouseholdContextService>();

        _navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
    }

    [Fact]
    public void Households_RendersEveryHouseholdOfTheCurrentUser()
    {
        ArrangeHouseholds();

        var cut = Render<Households>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Brown Household", cut.Find("[data-testid='household-option-0']").TextContent);
            Assert.Contains("Green Household", cut.Find("[data-testid='household-option-1']").TextContent);
        });

        // The list is the shared source, so it is requested exactly once through the context.
        Assert.Contains(HouseholdContextService.HouseholdsPath, _apiClient.Gets);
    }

    [Fact]
    public void Households_MarksTheCurrentHousehold()
    {
        ArrangeHouseholds();

        var cut = Render<Households>();

        cut.WaitForAssertion(() =>
        {
            // The first household by name is the shared default, so it is marked as current.
            Assert.Equal("true", cut.Find("[data-testid='household-option-0']").GetAttribute("aria-current"));
            Assert.Null(cut.Find("[data-testid='household-option-1']").GetAttribute("aria-current"));
        });

        Assert.Contains("Current household", cut.Find("[data-testid='household-option-0']").TextContent);
    }

    [Fact]
    public void Households_EachOptionExposesTheHouseholdNameToAssistiveTechnology()
    {
        ArrangeHouseholds();

        var cut = Render<Households>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(
                "Open Brown Household",
                cut.Find("[data-testid='household-option-0']").GetAttribute("aria-label"));
            Assert.Equal(
                "Open Green Household",
                cut.Find("[data-testid='household-option-1']").GetAttribute("aria-label"));
        });
    }

    [Fact]
    public void Households_PickingAHousehold_SelectsItAndOpensTheDashboard()
    {
        ArrangeHouseholds();

        var cut = Render<Households>();

        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll("[data-testid='household-option-1']")));

        cut.Find("[data-testid='household-option-1']").Click();

        HouseholdContextService householdContext = Services.GetRequiredService<HouseholdContextService>();
        Assert.Equal(SecondId, householdContext.SelectedHousehold!.Id);
        Assert.EndsWith("/dashboard", _navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Households_HouseholdWithLogo_RendersTheLogoImage()
    {
        ArrangeHouseholds();
        _apiClient.FileResponses[Households.LogoPathFor(SecondId)] =
            new FileContent("image/webp", new byte[] { 4, 5, 6 });

        var cut = Render<Households>();

        cut.WaitForAssertion(() =>
        {
            var logo = cut.Find("[data-testid='household-logo-1']");
            Assert.StartsWith("data:image/webp;base64,", logo.GetAttribute("src"), StringComparison.Ordinal);
        });

        // The household without a logo keeps its placeholder.
        Assert.NotEmpty(cut.FindAll("[data-testid='household-placeholder-0']"));
    }

    [Fact]
    public void Households_HouseholdWithoutLogo_RendersThePlaceholder()
    {
        ArrangeHouseholds();

        var cut = Render<Households>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("[data-testid='household-placeholder-0']"));
            Assert.NotEmpty(cut.FindAll("[data-testid='household-placeholder-1']"));
        });

        Assert.Empty(cut.FindAll("[data-testid='household-logo-0']"));
        Assert.Empty(cut.FindAll("[data-testid='household-logo-1']"));
    }

    [Fact]
    public void Households_LogoFailure_KeepsTheListAndShowsThePlaceholder()
    {
        ArrangeHouseholds();
        _apiClient.ThrowByPath[Households.LogoPathFor(FirstId)] =
            new ClientErrorException(ClientErrorCode.NotFound, FirstId);

        var cut = Render<Households>();

        cut.WaitForAssertion(() =>
        {
            // Branding is decorative: the list still renders both households.
            Assert.NotEmpty(cut.FindAll("[data-testid='household-option-0']"));
            Assert.NotEmpty(cut.FindAll("[data-testid='household-option-1']"));
            Assert.NotEmpty(cut.FindAll("[data-testid='household-placeholder-0']"));
        });

        Assert.Empty(cut.FindAll("[data-testid='households-error']"));
        Assert.Empty(cut.FindAll("[data-testid='household-logo-0']"));
    }

    [Fact]
    public void Households_NoHouseholds_ShowsTheEmptyStatePointingAtSetup()
    {
        _apiClient.GetResponsesByPath[HouseholdContextService.HouseholdsPath] = new List<Household>();

        var cut = Render<Households>();

        cut.WaitForAssertion(() =>
        {
            var empty = cut.Find("[data-testid='households-empty']");
            Assert.Contains(Households.EmptyStateMessage, empty.TextContent);
        });

        Assert.NotEmpty(cut.FindAll("[data-testid='households-setup']"));
        Assert.Empty(cut.FindAll("[data-testid='household-option-0']"));
    }

    [Fact]
    public void Households_LoadFailure_ShowsTheConventionMessage()
    {
        _apiClient.Throw = new ClientErrorException(ClientErrorCode.Unauthorized, 7);

        var cut = Render<Households>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[data-testid='households-error']");
            Assert.Equal("Unauthorized 7", alert.TextContent.Trim());
        });

        Assert.Empty(cut.FindAll("[data-testid='household-option-0']"));
        Assert.Empty(cut.FindAll("[data-testid='households-empty']"));
    }

    [Fact]
    public void Households_UnexpectedFailure_ShowsTheConventionMessage()
    {
        _apiClient.Throw = new InvalidOperationException("boom");

        var cut = Render<Households>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[data-testid='households-error']");
            Assert.Equal("Unknown InvalidOperationException", alert.TextContent.Trim());
        });
    }

    private void ArrangeHouseholds()
    {
        _apiClient.GetResponsesByPath[HouseholdContextService.HouseholdsPath] = new List<Household>
        {
            new Household { Id = FirstId, Name = "Brown Household" },
            new Household { Id = SecondId, Name = "Green Household" }
        };
    }
}

using Bunit;
using HouseholdFinances.Frontend.Components.Pages;
using HouseholdFinances.Frontend.Models;
using HouseholdFinances.Frontend.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace HouseholdFinances.Frontend.Tests.Components;

/// <summary>
/// bUnit component tests for the Household Dashboard (issue #12): the populated summary renders the
/// income, expense, and remaining-balance values from the household data; the empty state renders
/// guidance instead of a bare zero; and load failures surface through the shared client error
/// convention.
/// </summary>
/// <remarks>
/// The API client is replaced with a test double, so no live backend or browser is involved. The
/// tests prove the page requests the household list, formats the totals with the decided money
/// rules, and shows guidance or an error; they do not prove the real backend returns the totals or
/// that a browser renders the page.
/// </remarks>
public class DashboardPageTests : BunitContext
{
    private readonly StubApiClient _apiClient = new();

    public DashboardPageTests()
    {
        // MudBlazor components may touch JS interop; loose mode keeps render side-effect free.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IApiClient>(_apiClient);
    }

    [Fact]
    public void Dashboard_Populated_RendersFormattedIncomeExpensesAndBalance()
    {
        _apiClient.GetResponse = new List<Household>
        {
            new() { Id = Guid.NewGuid(), Name = "Family", Incomes = 5000, Payments = 1234.5 }
        };

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("$5,000.00", cut.Find("[data-testid='dashboard-income']").TextContent.Trim());
            Assert.Equal("$1,234.50", cut.Find("[data-testid='dashboard-expenses']").TextContent.Trim());
            Assert.Equal("$3,765.50", cut.Find("[data-testid='dashboard-balance']").TextContent.Trim());
        });

        // The dashboard loads the totals from the households endpoint.
        Assert.Equal(Dashboard.HouseholdsPath, Assert.Single(_apiClient.Gets));
    }

    [Fact]
    public void Dashboard_Populated_ShowsTheHouseholdName()
    {
        _apiClient.GetResponse = new List<Household>
        {
            new() { Id = Guid.NewGuid(), Name = "Family", Incomes = 5000, Payments = 1000 }
        };

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() => Assert.Contains("Family", cut.Markup));
    }

    [Fact]
    public void Dashboard_NegativeBalance_IsFormattedWithALeadingSign()
    {
        _apiClient.GetResponse = new List<Household>
        {
            new() { Id = Guid.NewGuid(), Name = "Family", Incomes = 100, Payments = 150 }
        };

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
            Assert.Equal("-$50.00", cut.Find("[data-testid='dashboard-balance']").TextContent.Trim()));
    }

    [Fact]
    public void Dashboard_MultipleHouseholds_SummarisesTheFirstOnePredictably()
    {
        var first = new Household { Id = Guid.NewGuid(), Name = "Alpha", Incomes = 3000, Payments = 1000 };
        var second = new Household { Id = Guid.NewGuid(), Name = "Beta", Incomes = 9000, Payments = 5000 };
        _apiClient.GetResponse = new List<Household> { first, second };

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            // The first household's totals win, not the second's, so the choice is deterministic.
            Assert.Equal("$3,000.00", cut.Find("[data-testid='dashboard-income']").TextContent.Trim());
            Assert.Equal("$2,000.00", cut.Find("[data-testid='dashboard-balance']").TextContent.Trim());
        });
    }

    [Fact]
    public void Dashboard_NoIncomeOrBills_RendersGuidanceInsteadOfZeros()
    {
        _apiClient.GetResponse = new List<Household>
        {
            new() { Id = Guid.NewGuid(), Name = "Family", Incomes = 0, Payments = 0 }
        };

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            var empty = cut.Find("[data-testid='dashboard-empty']");
            Assert.Contains(Dashboard.EmptyStateMessage, empty.TextContent);
        });

        // Guidance replaces the summary cards, so no bare zero is shown.
        Assert.Empty(cut.FindAll("[data-testid='dashboard-income']"));
        Assert.Empty(cut.FindAll("[data-testid='dashboard-expenses']"));
        Assert.Empty(cut.FindAll("[data-testid='dashboard-balance']"));
    }

    [Fact]
    public void Dashboard_NoHousehold_RendersNoHouseholdGuidance()
    {
        _apiClient.GetResponse = new List<Household>();

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            var empty = cut.Find("[data-testid='dashboard-no-household']");
            Assert.Contains(Dashboard.NoHouseholdMessage, empty.TextContent);
        });

        Assert.Empty(cut.FindAll("[data-testid='dashboard-income']"));
    }

    [Fact]
    public void Dashboard_LoadFailure_ShowsConventionMessageAndNoSummary()
    {
        _apiClient.Throw = new ClientErrorException(ClientErrorCode.Unauthorized, 7);

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[data-testid='dashboard-error']");
            Assert.Equal("Unauthorized 7", alert.TextContent.Trim());
        });

        Assert.Empty(cut.FindAll("[data-testid='dashboard-income']"));
    }

    [Fact]
    public void Dashboard_UnexpectedFailure_ShowsConventionMessage()
    {
        _apiClient.Throw = new InvalidOperationException("boom");

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[data-testid='dashboard-error']");
            Assert.Equal("Unknown InvalidOperationException", alert.TextContent.Trim());
        });
    }
}

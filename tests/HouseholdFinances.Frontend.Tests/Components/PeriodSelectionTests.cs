using Bunit;
using HouseholdFinances.Frontend.Models;
using HouseholdFinances.Frontend.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using BillsPage = HouseholdFinances.Frontend.Components.Pages.Bills;
using DashboardPage = HouseholdFinances.Frontend.Components.Pages.Dashboard;
using IncomeEntry = HouseholdFinances.Frontend.Models.Income;
using IncomePage = HouseholdFinances.Frontend.Components.Pages.Income;

namespace HouseholdFinances.Frontend.Tests.Components;

/// <summary>
/// bUnit tests for the shared period selection (issue #47). Both pages resolve the same scoped
/// <see cref="PeriodSelectionService"/>, so a period chosen on Bills is already selected when
/// Income opens in the same circuit. A full reload creates a new circuit, which starts again at
/// Monthly; the page-level default tests cover that starting state.
/// </summary>
/// <remarks>
/// The API client is a test double and no browser or live backend is involved. The two pages are
/// rendered in one bUnit context, which stands in for one Blazor Server circuit.
/// </remarks>
public class PeriodSelectionTests : BunitContext, IAsyncLifetime
{
    private readonly StubApiClient _apiClient = new();
    private readonly FixedTimeProvider _clock =
        new(new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero));
    private readonly PeriodSelectionService _periodSelection = new();

    public PeriodSelectionTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IApiClient>(_apiClient);
        Services.AddSingleton<TimeProvider>(_clock);
        Services.AddScoped<PeriodSelectionService>(_ => _periodSelection);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await ((IAsyncDisposable)this).DisposeAsync();

    [Fact]
    public async Task PeriodSelectedOnBills_IsSelectedOnIncomeInTheSameCircuit()
    {
        var household = new Household { Id = Guid.NewGuid(), Name = "Family" };
        _apiClient.GetResponsesByPath[BillsPage.HouseholdsPath] = new List<Household> { household };
        _apiClient.GetResponsesByPath[BillsPage.BillsPathFor(household.Id)] = FixtureBills();
        _apiClient.GetResponsesByPath[IncomePage.HouseholdsPath] = new List<Household> { household };
        _apiClient.GetResponsesByPath[IncomePage.IncomesPathFor(household.Id)] = FixtureIncome();

        var popover = Render<MudPopoverProvider>();

        var bills = Render<BillsPage>();
        bills.WaitForAssertion(() =>
            Assert.Equal("$400.00", bills.Find("[data-testid='bills-total']").TextContent.Trim()));

        // Switch Bills to the 14-day window that begins on the Monday of this week.
        var billsSelect = bills.FindComponent<MudSelect<Interval>>();
        await bills.InvokeAsync(() => billsSelect.Instance.OpenMenu());
        popover.WaitForAssertion(() =>
            Assert.NotEmpty(popover.FindAll("div.mud-list-item")));
        popover.FindAll("div.mud-list-item")
            .First(item => item.TextContent.Trim() == nameof(Interval.BiWeekly))
            .Click();
        bills.WaitForAssertion(() =>
            Assert.Equal("$300.00", bills.Find("[data-testid='bills-total']").TextContent.Trim()));

        // Income opens in the same circuit and already shows the shared selection and figures.
        var income = Render<IncomePage>();
        income.WaitForAssertion(() =>
            Assert.Single(income.FindAll("[data-testid='income-summary']")));

        Assert.Contains(
            nameof(Interval.BiWeekly),
            income.FindComponent<MudSelect<Interval>>().Markup);

        income.WaitForAssertion(() =>
            Assert.Equal("$300.00", income.Find("[data-testid='income-total']").TextContent.Trim()));
    }

    [Fact]
    public async Task PeriodSelectedOnDashboard_IsSelectedOnBillsInTheSameCircuit()
    {
        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = "Family",
            Incomes = 400.00,
            Payments = 100.00
        };

        _apiClient.GetResponsesByPath[DashboardPage.HouseholdsPathFor(Interval.Monthly)] =
            new List<Household> { household };
        _apiClient.GetResponsesByPath[DashboardPage.HouseholdsPathFor(Interval.BiWeekly)] =
            new List<Household> { household };
        _apiClient.GetResponsesByPath[BillsPage.HouseholdsPath] = new List<Household> { household };
        _apiClient.GetResponsesByPath[BillsPage.BillsPathFor(household.Id)] = FixtureBills();

        var popover = Render<MudPopoverProvider>();

        var dashboard = Render<DashboardPage>();
        dashboard.WaitForAssertion(() =>
            Assert.NotEmpty(dashboard.FindAll("[data-testid='dashboard-period']")));

        var select = dashboard.FindComponent<MudSelect<Interval>>();
        await dashboard.InvokeAsync(() => select.Instance.OpenMenu());
        popover.WaitForAssertion(() =>
            Assert.NotEmpty(popover.FindAll("div.mud-list-item")));
        popover.FindAll("div.mud-list-item")
            .First(item => item.TextContent.Trim() == nameof(Interval.BiWeekly))
            .Click();

        dashboard.WaitForAssertion(() => Assert.Equal(
            "$300.00", dashboard.Find("[data-testid='dashboard-balance']").TextContent.Trim()));

        // Bills opens in the same circuit and already shows the shared selection.
        var bills = Render<BillsPage>();
        bills.WaitForAssertion(() => Assert.Contains(
            nameof(Interval.BiWeekly),
            bills.FindComponent<MudSelect<Interval>>().Markup));

        bills.WaitForAssertion(() =>
            Assert.Equal("$300.00", bills.Find("[data-testid='bills-total']").TextContent.Trim()));
    }

    private static List<Bill> FixtureBills() =>
        new()
        {
            Bill("B1", 100.00, Interval.BiWeekly, new DateTime(2026, 1, 5)),
            Bill("B2", 200.00, Interval.Monthly, new DateTime(2026, 1, 20)),
            Bill("B3", 120.00, Interval.Yearly, new DateTime(2026, 2, 1))
        };

    private static List<IncomeEntry> FixtureIncome() =>
        new()
        {
            Income("B1", 100.00, Interval.BiWeekly, new DateTime(2026, 1, 5)),
            Income("B2", 200.00, Interval.Monthly, new DateTime(2026, 1, 20)),
            Income("B3", 120.00, Interval.Yearly, new DateTime(2026, 2, 1))
        };

    private static Bill Bill(string name, double value, Interval interval, DateTime startDate) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Value = value,
            StartDate = startDate,
            EndDate = new DateTime(2026, 12, 31),
            Recurring = true,
            Interval = interval,
            HouseholdId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };

    private static IncomeEntry Income(string name, double value, Interval interval, DateTime startDate) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Value = value,
            StartDate = startDate,
            EndDate = new DateTime(2026, 12, 31),
            Recurring = true,
            Interval = interval,
            UserId = Guid.NewGuid()
        };
}

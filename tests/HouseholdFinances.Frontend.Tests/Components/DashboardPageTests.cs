using Bunit;
using HouseholdFinances.Frontend.Components.Pages;
using HouseholdFinances.Frontend.Models;
using HouseholdFinances.Frontend.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace HouseholdFinances.Frontend.Tests.Components;

/// <summary>
/// bUnit component tests for the Household Dashboard: the populated summary renders the income,
/// expense, and remaining-balance values for the selected calendar period; the period control
/// offers the six intervals and defaults to Monthly; the empty state renders guidance instead of a
/// bare zero; and load failures surface through the shared client error convention.
/// </summary>
/// <remarks>
/// The API client is replaced with a test double, so no live backend or browser is involved. The
/// tests prove the page requests the period-qualified household list, renders the returned totals
/// unchanged, and shows guidance or an error; they do not prove the real backend windows the totals
/// or that a browser renders the page.
/// </remarks>
public class DashboardPageTests : BunitContext, IAsyncLifetime
{
    private readonly StubApiClient _apiClient = new();
    private readonly FixedTimeProvider _clock =
        new(new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero));
    private readonly PeriodSelectionService _periodSelection = new();

    public DashboardPageTests()
    {
        // MudBlazor components may touch JS interop; loose mode keeps render side-effect free.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IApiClient>(_apiClient);
        Services.AddSingleton<TimeProvider>(_clock);
        // The scoped service stands in for the circuit's single selection, so every page rendered
        // in one context shares it.
        Services.AddScoped<PeriodSelectionService>(_ => _periodSelection);
    }

    // The allocation panel renders MudBlazor inputs that resolve services which only implement
    // IAsyncDisposable, so the context must be disposed asynchronously. xUnit routes teardown
    // through IAsyncLifetime instead of the synchronous IDisposable.Dispose when it is implemented.
    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await ((IAsyncDisposable)this).DisposeAsync();

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

        // The dashboard loads the Monthly totals from the households endpoint.
        Assert.Equal(Dashboard.HouseholdsPathFor(Interval.Monthly), Assert.Single(_apiClient.Gets));
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

        // Guidance replaces the summary cards, so no bare zero is shown. The period control stays
        // available so a window with no occurrences can be switched to one that has some.
        Assert.Empty(cut.FindAll("[data-testid='dashboard-income']"));
        Assert.Empty(cut.FindAll("[data-testid='dashboard-expenses']"));
        Assert.Empty(cut.FindAll("[data-testid='dashboard-balance']"));
        Assert.NotEmpty(cut.FindAll("[data-testid='dashboard-period']"));
        Assert.Single(cut.FindComponents<MudSelect<Interval>>());
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
        Assert.Empty(cut.FindAll("[data-testid='dashboard-period']"));
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
        Assert.Empty(cut.FindAll("[data-testid='dashboard-period']"));
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

    [Fact]
    public void Dashboard_ValidAllocation_ShowsAllocationAndUnallocatedRemainder()
    {
        _apiClient.GetResponse = new List<Household>
        {
            new() { Id = Guid.NewGuid(), Name = "Family", Incomes = 5000, Payments = 3000 }
        };

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='dashboard-allocation']")));

        cut.Find("[data-testid='dashboard-allocation-savings']").Change("800");
        cut.Find("[data-testid='dashboard-allocation-fun']").Change("700");
        cut.Find("[data-testid='dashboard-allocation-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("$800.00", cut.Find("[data-testid='dashboard-allocation-savings-value']").TextContent.Trim());
            Assert.Equal("$700.00", cut.Find("[data-testid='dashboard-allocation-fun-value']").TextContent.Trim());
            // 2000 remaining - 800 savings - 700 fun money leaves 500 unallocated.
            Assert.Equal("$500.00", cut.Find("[data-testid='dashboard-allocation-remainder']").TextContent.Trim());
        });
    }

    [Fact]
    public void Dashboard_AllocationMatchingTheBalance_LeavesNoRemainder()
    {
        _apiClient.GetResponse = new List<Household>
        {
            new() { Id = Guid.NewGuid(), Name = "Family", Incomes = 2000, Payments = 500 }
        };

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='dashboard-allocation']")));

        // The split equals the whole remaining balance (1500), which is allowed.
        cut.Find("[data-testid='dashboard-allocation-savings']").Change("1000");
        cut.Find("[data-testid='dashboard-allocation-fun']").Change("500");
        cut.Find("[data-testid='dashboard-allocation-submit']").Click();

        cut.WaitForAssertion(() =>
            Assert.Equal("$0.00", cut.Find("[data-testid='dashboard-allocation-remainder']").TextContent.Trim()));
    }

    [Fact]
    public void Dashboard_OverAllocation_ShowsMessageAndKeepsTheSummaryIntact()
    {
        _apiClient.GetResponse = new List<Household>
        {
            new() { Id = Guid.NewGuid(), Name = "Family", Incomes = 5000, Payments = 3000 }
        };

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='dashboard-allocation']")));

        // 1500 + 1000 exceeds the 2000 remaining balance.
        cut.Find("[data-testid='dashboard-allocation-savings']").Change("1500");
        cut.Find("[data-testid='dashboard-allocation-fun']").Change("1000");
        cut.Find("[data-testid='dashboard-allocation-submit']").Click();

        cut.WaitForAssertion(() => Assert.Contains(
            Dashboard.AllocationExceedsBalanceMessage,
            cut.Find("[data-testid='dashboard-allocation-error']").TextContent));

        // The rejected split is not displayed, and the summary still renders without a crash.
        Assert.Empty(cut.FindAll("[data-testid='dashboard-allocation-result']"));
        Assert.Equal("$2,000.00", cut.Find("[data-testid='dashboard-balance']").TextContent.Trim());
    }

    [Fact]
    public void Dashboard_NegativeAllocation_ShowsMessageAndIsNotDisplayed()
    {
        _apiClient.GetResponse = new List<Household>
        {
            new() { Id = Guid.NewGuid(), Name = "Family", Incomes = 5000, Payments = 3000 }
        };

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='dashboard-allocation']")));

        cut.Find("[data-testid='dashboard-allocation-savings']").Change("-5");
        cut.Find("[data-testid='dashboard-allocation-submit']").Click();

        cut.WaitForAssertion(() => Assert.Contains(Dashboard.NegativeAmountMessage, cut.Markup));

        Assert.Empty(cut.FindAll("[data-testid='dashboard-allocation-result']"));
    }

    [Fact]
    public void Dashboard_Allocation_IsSessionOnlyAndMakesNoApiWrite()
    {
        _apiClient.GetResponse = new List<Household>
        {
            new() { Id = Guid.NewGuid(), Name = "Family", Incomes = 5000, Payments = 3000 }
        };

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='dashboard-allocation']")));

        cut.Find("[data-testid='dashboard-allocation-savings']").Change("800");
        cut.Find("[data-testid='dashboard-allocation-fun']").Change("700");
        cut.Find("[data-testid='dashboard-allocation-submit']").Click();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='dashboard-allocation-result']")));

        // The split is session-only page state: accepting it makes no API write, and the only
        // request the page ever makes is the initial Monthly household load.
        Assert.Empty(_apiClient.Posts);
        Assert.Empty(_apiClient.Puts);
        Assert.Empty(_apiClient.Patches);
        Assert.Empty(_apiClient.Deletes);
        Assert.Equal(Dashboard.HouseholdsPathFor(Interval.Monthly), Assert.Single(_apiClient.Gets));
    }

    [Fact]
    public void Dashboard_FirstLoad_DefaultsToMonthlyAndRequestsTheMonthlyPath()
    {
        ArrangeHouseholdsForEveryPeriod();

        IRenderedComponent<Dashboard> cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("[data-testid='dashboard-period']"));
            Assert.Single(cut.FindComponents<MudSelect<Interval>>());
        });

        Assert.Equal(Interval.Monthly, _periodSelection.Period);
        Assert.Contains(
            nameof(Interval.Monthly),
            cut.FindComponent<MudSelect<Interval>>().Markup);
        Assert.Equal(Dashboard.HouseholdsPathFor(Interval.Monthly), Assert.Single(_apiClient.Gets));
    }

    [Fact]
    public async Task Dashboard_PeriodControl_OffersTheSixIntervals()
    {
        ArrangeHouseholdsForEveryPeriod();

        // MudBlazor renders the options of a MudSelect inside MudPopoverProvider. The layout
        // supplies it in the running app, so it has to be rendered explicitly here.
        IRenderedComponent<MudPopoverProvider> popover = Render<MudPopoverProvider>();

        IRenderedComponent<Dashboard> cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotEmpty(cut.FindAll("[data-testid='dashboard-period']"));
            Assert.Single(cut.FindComponents<MudSelect<Interval>>());
        });

        IRenderedComponent<MudSelect<Interval>> select = cut.FindComponent<MudSelect<Interval>>();
        await cut.InvokeAsync(() => select.Instance.OpenMenu());

        popover.WaitForAssertion(() => Assert.NotEmpty(popover.FindAll("div.mud-list-item")));

        string[] expectedOptions = { "Daily", "Weekly", "BiWeekly", "Monthly", "Quarterly", "Yearly" };

        Assert.Equal(
            expectedOptions,
            popover.FindAll("div.mud-list-item")
                .Select(item => item.TextContent.Trim())
                .ToList());
    }

    [Theory]
    [InlineData(Interval.Daily)]
    [InlineData(Interval.Weekly)]
    [InlineData(Interval.BiWeekly)]
    [InlineData(Interval.Monthly)]
    [InlineData(Interval.Quarterly)]
    [InlineData(Interval.Yearly)]
    public async Task Dashboard_EachPeriod_RequestsThatPathAndRendersOnlyTheStubbedFigures(
        Interval period)
    {
        ArrangeHouseholdsForEveryPeriod();

        IRenderedComponent<MudPopoverProvider> popover = Render<MudPopoverProvider>();

        IRenderedComponent<Dashboard> cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
            Assert.Equal(Dashboard.HouseholdsPathFor(Interval.Monthly), _apiClient.Gets[0]));

        if (period != Interval.Monthly)
        {
            await SelectPeriodAsync(cut, popover, period);
        }

        (double incomes, double payments) = FiguresFor(period);
        string expectedPath = Dashboard.HouseholdsPathFor(period);

        cut.WaitForAssertion(() =>
        {
            // Exactly one GET for the selected period, and it is the most recent request.
            Assert.Single(_apiClient.Gets, path => path == expectedPath);
            Assert.Equal(expectedPath, _apiClient.Gets[^1]);

            // The figures are the stubbed API values formatted by MoneyFormatter; the page does no
            // local recurrence expansion or re-summing.
            Assert.Equal(
                MoneyFormatter.Format(incomes),
                cut.Find("[data-testid='dashboard-income']").TextContent.Trim());
            Assert.Equal(
                MoneyFormatter.Format(payments),
                cut.Find("[data-testid='dashboard-expenses']").TextContent.Trim());
            Assert.Equal(
                MoneyFormatter.Format(incomes - payments),
                cut.Find("[data-testid='dashboard-balance']").TextContent.Trim());
        });

        // Monthly is already selected on first load, so it needs no second request.
        Assert.Equal(period == Interval.Monthly ? 1 : 2, _apiClient.Gets.Count);

        // Changing the period never writes.
        Assert.Empty(_apiClient.Posts);
        Assert.Empty(_apiClient.Puts);
        Assert.Empty(_apiClient.Patches);
        Assert.Empty(_apiClient.Deletes);
    }

    [Fact]
    public async Task Dashboard_PeriodChange_ClearsTheAcceptedAllocation()
    {
        ArrangeHouseholdsForEveryPeriod();

        IRenderedComponent<MudPopoverProvider> popover = Render<MudPopoverProvider>();

        IRenderedComponent<Dashboard> cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
            Assert.Single(cut.FindAll("[data-testid='dashboard-allocation']")));

        // Monthly figures are 430 in, 30 out, so 400 is available to split.
        cut.Find("[data-testid='dashboard-allocation-savings']").Change("100");
        cut.Find("[data-testid='dashboard-allocation-fun']").Change("50");
        cut.Find("[data-testid='dashboard-allocation-submit']").Click();

        cut.WaitForAssertion(() =>
            Assert.Single(cut.FindAll("[data-testid='dashboard-allocation-result']")));

        await SelectPeriodAsync(cut, popover, Interval.Weekly);

        cut.WaitForAssertion(() =>
        {
            // The split was validated against the Monthly balance, so it is not shown against the
            // Weekly one.
            Assert.Empty(cut.FindAll("[data-testid='dashboard-allocation-result']"));
            Assert.Empty(cut.FindAll("[data-testid='dashboard-allocation-error']"));
            Assert.Equal(
                "$150.00",
                cut.Find("[data-testid='dashboard-balance']").TextContent.Trim());
        });
    }

    [Fact]
    public async Task Dashboard_PeriodChange_ClearsTheAllocationErrorMessage()
    {
        ArrangeHouseholdsForEveryPeriod();

        IRenderedComponent<MudPopoverProvider> popover = Render<MudPopoverProvider>();

        IRenderedComponent<Dashboard> cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
            Assert.Single(cut.FindAll("[data-testid='dashboard-allocation']")));

        // 400 + 100 exceeds the 400 Monthly balance, so the split is rejected with a message.
        cut.Find("[data-testid='dashboard-allocation-savings']").Change("400");
        cut.Find("[data-testid='dashboard-allocation-fun']").Change("100");
        cut.Find("[data-testid='dashboard-allocation-submit']").Click();

        cut.WaitForAssertion(() => Assert.Contains(
            Dashboard.AllocationExceedsBalanceMessage,
            cut.Find("[data-testid='dashboard-allocation-error']").TextContent));

        await SelectPeriodAsync(cut, popover, Interval.Weekly);

        cut.WaitForAssertion(() => Assert.DoesNotContain(
            Dashboard.AllocationExceedsBalanceMessage,
            cut.Markup));
        Assert.Empty(cut.FindAll("[data-testid='dashboard-allocation-error']"));
        Assert.Empty(cut.FindAll("[data-testid='dashboard-allocation-result']"));
    }

    [Fact]
    public async Task Dashboard_PeriodChange_ClearsTheAllocationInputErrors()
    {
        ArrangeHouseholdsForEveryPeriod();

        IRenderedComponent<MudPopoverProvider> popover = Render<MudPopoverProvider>();

        IRenderedComponent<Dashboard> cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
            Assert.Single(cut.FindAll("[data-testid='dashboard-allocation']")));

        cut.Find("[data-testid='dashboard-allocation-savings']").Change("-5");
        cut.Find("[data-testid='dashboard-allocation-submit']").Click();

        cut.WaitForAssertion(() => Assert.Contains(Dashboard.NegativeAmountMessage, cut.Markup));

        await SelectPeriodAsync(cut, popover, Interval.Quarterly);

        cut.WaitForAssertion(() => Assert.DoesNotContain(
            Dashboard.NegativeAmountMessage,
            cut.Markup));
        Assert.Empty(cut.FindAll("[data-testid='dashboard-allocation-result']"));
    }

    /// <summary>
    /// Selects a period through the dashboard control, exactly as a user would.
    /// </summary>
    /// <param name="cut">The rendered Dashboard.</param>
    /// <param name="popover">The rendered popover host that holds the options.</param>
    /// <param name="period">The period to select.</param>
    private static async Task SelectPeriodAsync(
        IRenderedComponent<Dashboard> cut,
        IRenderedComponent<MudPopoverProvider> popover,
        Interval period)
    {
        IRenderedComponent<MudSelect<Interval>> select = cut.FindComponent<MudSelect<Interval>>();
        await cut.InvokeAsync(() => select.Instance.OpenMenu());

        popover.WaitForAssertion(() =>
            Assert.NotEmpty(popover.FindAll("div.mud-list-item")));

        popover.FindAll("div.mud-list-item")
            .First(item => item.TextContent.Trim() == period.ToString())
            .Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(period.ToString(), cut.FindComponent<MudSelect<Interval>>().Markup));
    }

    // Every period path returns a distinct household, so the rendered figures prove which response
    // the page applied. BiWeekly is deliberately negative.
    private static (double Incomes, double Payments) FiguresFor(Interval period) => period switch
    {
        Interval.Daily => (100.00, 25.50),
        Interval.Weekly => (210.00, 60.00),
        Interval.BiWeekly => (320.00, 500.00),
        Interval.Monthly => (430.00, 30.00),
        Interval.Quarterly => (540.00, 40.00),
        Interval.Yearly => (650.00, 50.00),
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };

    private void ArrangeHouseholdsForEveryPeriod()
    {
        foreach (Interval period in Dashboard.Intervals)
        {
            (double incomes, double payments) = FiguresFor(period);

            _apiClient.GetResponsesByPath[Dashboard.HouseholdsPathFor(period)] =
                new List<Household>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Name = $"Family {period}",
                        Incomes = incomes,
                        Payments = payments
                    }
                };
        }
    }
}

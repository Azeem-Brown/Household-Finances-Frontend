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
/// bare zero only when the household has no entries at all; and load failures surface through the
/// shared client error convention.
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
        // The household picker and the shell share this service, so the dashboard reads the same
        // selection they do.
        Services.AddScoped<HouseholdContextService>();
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
            HouseholdWithTotals("Family", 5000, 1234.5)
        };

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("$5,000.00", cut.Find("[data-testid='dashboard-income']").TextContent.Trim());
            Assert.Equal("$1,234.50", cut.Find("[data-testid='dashboard-expenses']").TextContent.Trim());
            Assert.Equal("$3,765.50", cut.Find("[data-testid='dashboard-balance']").TextContent.Trim());
        });

        // The dashboard loads the Monthly totals from the households endpoint, then the household's
        // unwindowed goals.
        Assert.Equal(Dashboard.HouseholdsPathFor(Interval.Monthly), _apiClient.Gets[0]);
        Assert.Equal(2, _apiClient.Gets.Count);
        Assert.EndsWith("/goals", _apiClient.Gets[1]);
    }

    [Fact]
    public void Dashboard_Populated_ShowsTheHouseholdName()
    {
        _apiClient.GetResponse = new List<Household>
        {
            HouseholdWithTotals("Family", 5000, 1000)
        };

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() => Assert.Contains("Family", cut.Markup));
    }

    [Fact]
    public void Dashboard_NegativeBalance_IsFormattedWithALeadingSign()
    {
        _apiClient.GetResponse = new List<Household>
        {
            HouseholdWithTotals("Family", 100, 150)
        };

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
            Assert.Equal("-$50.00", cut.Find("[data-testid='dashboard-balance']").TextContent.Trim()));
    }

    [Fact]
    public async Task Dashboard_MultipleHouseholds_UsesTheHouseholdPickedOnTheHouseholdList()
    {
        Household first = HouseholdWithTotals("Alpha", 3000, 1000);
        Household second = HouseholdWithTotals("Beta", 9000, 5000);
        _apiClient.GetResponse = new List<Household> { first, second };

        // Stand in for the picker having selected the second household in this circuit.
        HouseholdContextService householdContext = Services.GetRequiredService<HouseholdContextService>();
        await householdContext.EnsureLoadedAsync();
        householdContext.Select(second.Id);

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            // The picked household's totals win, not the first household in the API response.
            Assert.Equal("$9,000.00", cut.Find("[data-testid='dashboard-income']").TextContent.Trim());
            Assert.Equal("$4,000.00", cut.Find("[data-testid='dashboard-balance']").TextContent.Trim());
        });

        Assert.Contains("Beta", cut.Markup);
    }

    [Fact]
    public void Dashboard_MultipleHouseholds_DefaultsToTheFirstWhenNothingIsPicked()
    {
        Household first = HouseholdWithTotals("Alpha", 3000, 1000);
        Household second = HouseholdWithTotals("Beta", 9000, 5000);
        _apiClient.GetResponse = new List<Household> { first, second };

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            // Reaching the dashboard without a pick is still deterministic: the first household by
            // name is summarised, which is what the shared selection defaults to as well.
            Assert.Equal("$3,000.00", cut.Find("[data-testid='dashboard-income']").TextContent.Trim());
            Assert.Equal("$2,000.00", cut.Find("[data-testid='dashboard-balance']").TextContent.Trim());
        });
    }

    [Fact]
    public void Dashboard_HouseholdWithLogo_RendersTheLogoInTheHeader()
    {
        // The logo endpoint is the same one the shell and the household list use.
        Household household = HouseholdWithTotals("Family", 5000, 1000);
        _apiClient.GetResponse = new List<Household> { household };
        _apiClient.FileResponses[Dashboard.LogoPathFor(household.Id)] =
            new FileContent("image/png", new byte[] { 1, 2, 3 });

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            IElement logo = cut.Find("[data-testid='dashboard-logo']");
            Assert.StartsWith(
                "data:image/png;base64,",
                logo.GetAttribute("src"),
                StringComparison.Ordinal);
        });

        Assert.Empty(cut.FindAll("[data-testid='dashboard-logo-placeholder']"));
    }

    [Fact]
    public void Dashboard_HouseholdWithoutLogo_RendersThePlaceholder()
    {
        _apiClient.GetResponse = new List<Household>
        {
            HouseholdWithTotals("Family", 5000, 1000)
        };

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
            Assert.NotEmpty(cut.FindAll("[data-testid='dashboard-logo-placeholder']")));
        Assert.Empty(cut.FindAll("[data-testid='dashboard-logo']"));
    }

    [Fact]
    public void Dashboard_LogoFailure_KeepsTheSummaryAndShowsThePlaceholder()
    {
        Household household = HouseholdWithTotals("Family", 5000, 1000);
        _apiClient.GetResponse = new List<Household> { household };
        _apiClient.ThrowByPath[Dashboard.LogoPathFor(household.Id)] =
            new ClientErrorException(ClientErrorCode.NotFound, household.Id);

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            // Branding is decorative, so the summary still renders and only the image is missing.
            Assert.Equal("$5,000.00", cut.Find("[data-testid='dashboard-income']").TextContent.Trim());
            Assert.NotEmpty(cut.FindAll("[data-testid='dashboard-logo-placeholder']"));
        });

        Assert.Empty(cut.FindAll("[data-testid='dashboard-error']"));
        Assert.Empty(cut.FindAll("[data-testid='dashboard-logo']"));
    }

    [Fact]
    public void Dashboard_NoEntries_RendersGuidanceInsteadOfZeros()
    {
        // No stored entries at all: both window-independent counts are zero, so this is the only
        // case that shows the onboarding guidance.
        _apiClient.GetResponse = new List<Household>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Family",
                Incomes = 0,
                Payments = 0,
                IncomeEntryCount = 0,
                BillEntryCount = 0
            }
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

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    [InlineData(2, 3)]
    public void Dashboard_EntriesOutsideTheSelectedPeriod_RendersZerosInsteadOfGuidance(
        int incomeEntryCount,
        int billEntryCount)
    {
        // The household has stored entries, but none of them fall in the selected window, so the
        // API returns zero window totals while the entry counts stay non-zero.
        _apiClient.GetResponse = new List<Household>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Family",
                Incomes = 0,
                Payments = 0,
                IncomeEntryCount = incomeEntryCount,
                BillEntryCount = billEntryCount
            }
        };

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("$0.00", cut.Find("[data-testid='dashboard-income']").TextContent.Trim());
            Assert.Equal("$0.00", cut.Find("[data-testid='dashboard-expenses']").TextContent.Trim());
            Assert.Equal("$0.00", cut.Find("[data-testid='dashboard-balance']").TextContent.Trim());
        });

        // The guidance card belongs to a household with no entries, not to an empty window.
        Assert.Empty(cut.FindAll("[data-testid='dashboard-empty']"));
        Assert.DoesNotContain(Dashboard.EmptyStateMessage, cut.Markup);

        // The period control and the split panel stay as they were for a populated household.
        Assert.NotEmpty(cut.FindAll("[data-testid='dashboard-period']"));
        Assert.Single(cut.FindAll("[data-testid='dashboard-allocation']"));
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
            HouseholdWithTotals("Family", 5000, 3000)
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
            HouseholdWithTotals("Family", 2000, 500)
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
            HouseholdWithTotals("Family", 5000, 3000)
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
            HouseholdWithTotals("Family", 5000, 3000)
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
            HouseholdWithTotals("Family", 5000, 3000)
        };

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='dashboard-allocation']")));

        cut.Find("[data-testid='dashboard-allocation-savings']").Change("800");
        cut.Find("[data-testid='dashboard-allocation-fun']").Change("700");
        cut.Find("[data-testid='dashboard-allocation-submit']").Click();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='dashboard-allocation-result']")));

        // The split is session-only page state: accepting it makes no API write, and the only
        // requests the page makes are the initial Monthly household load and its goals.
        Assert.Empty(_apiClient.Posts);
        Assert.Empty(_apiClient.Puts);
        Assert.Empty(_apiClient.Patches);
        Assert.Empty(_apiClient.Deletes);
        Assert.Equal(Dashboard.HouseholdsPathFor(Interval.Monthly), _apiClient.Gets[0]);
        Assert.Equal(2, _apiClient.Gets.Count);
        Assert.EndsWith("/goals", _apiClient.Gets[1]);
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
        Assert.Equal(Dashboard.HouseholdsPathFor(Interval.Monthly), _apiClient.Gets[0]);
        Assert.Equal(2, _apiClient.Gets.Count);
        Assert.EndsWith("/goals", _apiClient.Gets[1]);
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
            // Exactly one GET for the selected period, and it is the most recent period-qualified
            // household request. Goal paths start with the same prefix but carry no period query.
            Assert.Single(_apiClient.Gets, path => path == expectedPath);
            Assert.Equal(
                expectedPath,
                _apiClient.Gets.Last(
                    path => path.StartsWith($"{Dashboard.HouseholdsPath}?", StringComparison.Ordinal)));

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

        // Monthly is already selected on first load, so it needs no second household request. Goals
        // are fetched once per household, not per period.
        Assert.Equal(period == Interval.Monthly ? 2 : 3, _apiClient.Gets.Count);

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

    [Fact]
    public void Dashboard_Goal_RendersTargetCurrentTotalAndShortfall()
    {
        ArrangeHouseholdWithGoal(target: 10000, total: 2500);

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("$10,000.00", cut.Find("[data-testid='dashboard-goal-target']").TextContent.Trim());
            Assert.Equal("$2,500.00", cut.Find("[data-testid='dashboard-goal-total']").TextContent.Trim());
            Assert.Equal("$7,500.00", cut.Find("[data-testid='dashboard-goal-shortfall']").TextContent.Trim());
        });

        // Without an accepted savings allocation the page explains why no time is shown rather
        // than dividing by zero.
        Assert.Contains(
            Dashboard.NoContributionMessage,
            cut.Find("[data-testid='dashboard-goal-message']").TextContent);
        Assert.Empty(cut.FindAll("[data-testid='dashboard-goal-time']"));
    }

    [Fact]
    public void Dashboard_Goal_CurrentPositionInputUpdatesTheShortfall()
    {
        ArrangeHouseholdWithGoal(target: 10000, total: 2500);

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='dashboard-goal']")));

        // The initial shortfall uses the stored current total (2500); editing the current position
        // recomputes it without any API write. Immediate binding listens to oninput, not onchange.
        cut.Find("[data-testid='dashboard-goal-position']").Input("4000");

        cut.WaitForAssertion(() => Assert.Equal(
            "$6,000.00",
            cut.Find("[data-testid='dashboard-goal-shortfall']").TextContent.Trim()));
    }

    [Fact]
    public void Dashboard_Goal_PositiveSavingsAllocationShowsTimeToGoal()
    {
        ArrangeHouseholdWithGoal(target: 10000, total: 0);

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='dashboard-allocation']")));

        cut.Find("[data-testid='dashboard-allocation-savings']").Change("1000");
        cut.Find("[data-testid='dashboard-allocation-submit']").Click();

        cut.WaitForAssertion(() => Assert.Equal(
            "10 months",
            cut.Find("[data-testid='dashboard-goal-time']").TextContent.Trim()));

        Assert.Empty(cut.FindAll("[data-testid='dashboard-goal-message']"));
    }

    [Fact]
    public void Dashboard_Goal_ZeroSavingsAllocationShowsMessageInsteadOfDividing()
    {
        ArrangeHouseholdWithGoal(target: 10000, total: 0);

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='dashboard-allocation']")));

        // Accepting a zero split is valid; the projection must explain that no contribution is
        // available rather than dividing by zero.
        cut.Find("[data-testid='dashboard-allocation-submit']").Click();

        cut.WaitForAssertion(() => Assert.Contains(
            Dashboard.NoContributionMessage,
            cut.Find("[data-testid='dashboard-goal-message']").TextContent));
        Assert.Empty(cut.FindAll("[data-testid='dashboard-goal-time']"));
    }

    [Fact]
    public void Dashboard_Goal_AlreadyReached_ShowsMessageAndNoTime()
    {
        ArrangeHouseholdWithGoal(target: 1000, total: 1200);

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='dashboard-allocation']")));

        // A positive contribution must not produce a time to goal once the goal is reached.
        cut.Find("[data-testid='dashboard-allocation-savings']").Change("500");
        cut.Find("[data-testid='dashboard-allocation-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("-$200.00", cut.Find("[data-testid='dashboard-goal-shortfall']").TextContent.Trim());
            Assert.Contains(
                "This goal has already been reached by $200.00.",
                cut.Find("[data-testid='dashboard-goal-message']").TextContent);
        });
        Assert.Empty(cut.FindAll("[data-testid='dashboard-goal-time']"));
    }

    [Fact]
    public void Dashboard_Goal_NoGoals_RendersNoGoalSection()
    {
        _apiClient.GetResponse = new List<Household>
        {
            HouseholdWithTotals("Family", 5000, 3000)
        };

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='dashboard-balance']")));

        // No goal exists, so the page does not invent a goal card or empty-state copy.
        Assert.Empty(cut.FindAll("[data-testid='dashboard-goal']"));
    }

    [Fact]
    public void Dashboard_Goal_LoadFailure_ShowsConventionMessageAndKeepsTheSummary()
    {
        Household household = HouseholdWithTotals("Family", 5000, 3000);
        _apiClient.GetResponsesByPath[Dashboard.HouseholdsPathFor(Interval.Monthly)] =
            new List<Household> { household };
        _apiClient.ThrowByPath[Dashboard.GoalsPathFor(household.Id)] =
            new ClientErrorException(ClientErrorCode.NotFound, 42);

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() => Assert.Equal(
            "NotFound 42",
            cut.Find("[data-testid='dashboard-goal-error']").TextContent.Trim()));

        // A goal failure is isolated: the summary and allocation still render, and the page error
        // is not used for the goal request.
        Assert.Equal("$5,000.00", cut.Find("[data-testid='dashboard-income']").TextContent.Trim());
        Assert.Single(cut.FindAll("[data-testid='dashboard-allocation']"));
        Assert.Empty(cut.FindAll("[data-testid='dashboard-error']"));
    }

    [Fact]
    public void Dashboard_Goal_MultipleGoals_ShowsTheFirstByName()
    {
        Household household = ArrangeHouseholdWithGoal(target: 1000, total: 0);
        _apiClient.GoalsResponse = new List<Goal>
        {
            GoalFor(household.Id, "Zeta", value: 1000, total: 0),
            GoalFor(household.Id, "Alpha", value: 2000, total: 0)
        };

        var cut = Render<Dashboard>();

        cut.WaitForAssertion(() => Assert.Equal(
            "Alpha",
            cut.Find("[data-testid='dashboard-goal-name']").TextContent.Trim()));
        Assert.Equal("$2,000.00", cut.Find("[data-testid='dashboard-goal-target']").TextContent.Trim());
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

    // The API returns the window totals alongside the window-independent entry counts, so a
    // fixture with non-zero totals always carries non-zero counts.
    private static Household HouseholdWithTotals(
        string name,
        double incomes,
        double payments,
        int incomeEntryCount = 1,
        int billEntryCount = 1,
        Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Incomes = incomes,
            Payments = payments,
            IncomeEntryCount = incomeEntryCount,
            BillEntryCount = billEntryCount
        };

    // The dashboard loads the goal section after the household, so the household response is
    // registered by path and the goal list is supplied through the stub's goal fallback.
    private Household ArrangeHouseholdWithGoal(
        double target,
        double total,
        string name = "Holiday")
    {
        Household household = HouseholdWithTotals("Family", 5000, 3000);
        _apiClient.GetResponsesByPath[Dashboard.HouseholdsPathFor(Interval.Monthly)] =
            new List<Household> { household };
        _apiClient.GoalsResponse = new List<Goal> { GoalFor(household.Id, name, target, total) };

        return household;
    }

    private static Goal GoalFor(Guid householdId, string name, double value, double total) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Value = value,
            Total = total,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            Recurring = false,
            Interval = null,
            HouseholdId = householdId,
            UserId = Guid.NewGuid()
        };

    private void ArrangeHouseholdsForEveryPeriod()
    {
        // The same household is returned for every period (only its window totals differ), so the
        // dashboard's once-per-household goal load is not triggered again by a period change.
        Guid householdId = Guid.NewGuid();

        foreach (Interval period in Dashboard.Intervals)
        {
            (double incomes, double payments) = FiguresFor(period);

            _apiClient.GetResponsesByPath[Dashboard.HouseholdsPathFor(period)] =
                new List<Household>
                {
                    HouseholdWithTotals($"Family {period}", incomes, payments, id: householdId)
                };
        }
    }
}

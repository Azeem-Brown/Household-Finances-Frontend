using Bunit;
using HouseholdFinances.Frontend.Models;
using HouseholdFinances.Frontend.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using BillsPage = HouseholdFinances.Frontend.Components.Pages.Bills;

namespace HouseholdFinances.Frontend.Tests.Components;

/// <summary>
/// bUnit component tests for the Bills page (issue #14): the populated state renders each bill's
/// label, value, dates, and recurring state; the summary equals the sum of the listed bills; and
/// the empty state renders a call to action instead of a bare table. Load failures surface through
/// the shared client error convention.
/// </summary>
/// <remarks>
/// The API client is replaced with a test double, so no live backend or browser is involved. The
/// tests prove the page resolves the current household, requests that household's bills, formats
/// the bills and total with the decided money rules, and shows guidance or an error. They do not
/// prove the real backend returns the bills or that a browser renders the page.
/// </remarks>
public class BillsPageTests : BunitContext
{
    private readonly StubApiClient _apiClient = new();

    public BillsPageTests()
    {
        // MudBlazor components may touch JS interop; loose mode keeps render side-effect free.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IApiClient>(_apiClient);
    }

    [Fact]
    public void Bills_Populated_RendersEachBillWithItsLabelAndValue()
    {
        ArrangeHouseholdWith(
            SampleBill("Rent", 1200.00),
            SampleBill("Internet", 59.99));

        var cut = Render<BillsPage>();

        cut.WaitForAssertion(() =>
        {
            var names = cut.FindAll("[data-testid='bills-name']")
                .Select(cell => cell.TextContent.Trim())
                .ToList();
            Assert.Contains("Rent", names);
            Assert.Contains("Internet", names);

            var values = cut.FindAll("[data-testid='bills-value']")
                .Select(cell => cell.TextContent.Trim())
                .ToList();
            Assert.Contains("$1,200.00", values);
            Assert.Contains("$59.99", values);
        });
    }

    [Fact]
    public void Bills_Populated_SummaryEqualsTheSumOfTheListedBills()
    {
        var bills = new[]
        {
            SampleBill("Rent", 1200.00),
            SampleBill("Internet", 59.99),
            SampleBill("Insurance", 240.01)
        };
        ArrangeHouseholdWith(bills);

        var cut = Render<BillsPage>();

        // The expected total is derived from the same bills, so the assertion proves the summary is
        // the sum of the listed bills, not a hard-coded value.
        var expectedTotal = MoneyFormatter.Format(bills.Sum(bill => bill.Value));

        cut.WaitForAssertion(() =>
            Assert.Equal(expectedTotal, cut.Find("[data-testid='bills-total']").TextContent.Trim()));

        Assert.Equal("$1,500.00", expectedTotal);
    }

    [Fact]
    public void Bills_Populated_ShowsDatesAndRecurringState()
    {
        ArrangeHouseholdWith(
            SampleBill("Rent", 1200.00, recurring: true, Interval.Monthly),
            SampleBill("Car repair", 480.00, recurring: false, interval: null));

        var cut = Render<BillsPage>();

        cut.WaitForAssertion(() =>
        {
            var starts = cut.FindAll("[data-testid='bills-start']")
                .Select(cell => cell.TextContent.Trim())
                .ToList();
            Assert.All(starts, start => Assert.Equal("2026-01-01", start));

            var ends = cut.FindAll("[data-testid='bills-end']")
                .Select(cell => cell.TextContent.Trim())
                .ToList();
            Assert.All(ends, end => Assert.Equal("2026-12-31", end));

            var recurrence = cut.FindAll("[data-testid='bills-recurring']")
                .Select(cell => cell.TextContent.Trim())
                .ToList();
            Assert.Contains("Recurring (Monthly)", recurrence);
            Assert.Contains("One-off", recurrence);
        });
    }

    [Fact]
    public void Bills_Populated_ResolvesTheHouseholdThenLoadsItsBills()
    {
        var household = ArrangeHouseholdWith(SampleBill("Rent", 1200.00));

        var cut = Render<BillsPage>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='bills-table']")));

        // The page resolves the household first, then lists bills for that household only.
        Assert.Equal(
            new[] { BillsPage.HouseholdsPath, BillsPage.BillsPathFor(household.Id) },
            _apiClient.Gets);
    }

    [Fact]
    public void Bills_Empty_RendersCallToActionInsteadOfABareTable()
    {
        ArrangeHouseholdWith();

        var cut = Render<BillsPage>();

        cut.WaitForAssertion(() =>
        {
            var empty = cut.Find("[data-testid='bills-empty']");
            Assert.Contains(BillsPage.EmptyStateMessage, empty.TextContent);
            Assert.Contains("Add bill", cut.Find("[data-testid='bills-add']").TextContent);
        });

        // Guidance replaces the table, so no bare empty table is shown.
        Assert.Empty(cut.FindAll("[data-testid='bills-table']"));
        Assert.Empty(cut.FindAll("[data-testid='bills-total']"));
    }

    [Fact]
    public void Bills_NoHousehold_RendersGuidanceAndDoesNotRequestBills()
    {
        _apiClient.GetResponsesByPath[BillsPage.HouseholdsPath] = new List<Household>();

        var cut = Render<BillsPage>();

        cut.WaitForAssertion(() =>
        {
            var empty = cut.Find("[data-testid='bills-no-household']");
            Assert.Contains(BillsPage.NoHouseholdMessage, empty.TextContent);
        });

        // With no household there is nothing to nest bills under, so only the list call is made.
        Assert.Equal(new[] { BillsPage.HouseholdsPath }, _apiClient.Gets);
    }

    [Fact]
    public void Bills_HouseholdLoadFailure_ShowsConventionMessageAndNoTable()
    {
        _apiClient.Throw = new ClientErrorException(ClientErrorCode.Unauthorized, 7);

        var cut = Render<BillsPage>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[data-testid='bills-error']");
            Assert.Equal("Unauthorized 7", alert.TextContent.Trim());
        });

        Assert.Empty(cut.FindAll("[data-testid='bills-table']"));
    }

    [Fact]
    public void Bills_NestedBillsFailure_ShowsConventionMessageAndNoTable()
    {
        var household = new Household { Id = Guid.NewGuid(), Name = "Family" };
        _apiClient.GetResponsesByPath[BillsPage.HouseholdsPath] = new List<Household> { household };

        // The backend reports a household the user does not belong to as not found, which the shared
        // convention turns into "NotFound <id>".
        _apiClient.ThrowByPath[BillsPage.BillsPathFor(household.Id)] =
            new ClientErrorException(ClientErrorCode.NotFound, household.Id);

        var cut = Render<BillsPage>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[data-testid='bills-error']");
            Assert.Equal($"NotFound {household.Id}", alert.TextContent.Trim());
        });

        Assert.Empty(cut.FindAll("[data-testid='bills-table']"));
    }

    [Fact]
    public void Bills_UnexpectedFailure_ShowsConventionMessage()
    {
        _apiClient.Throw = new InvalidOperationException("boom");

        var cut = Render<BillsPage>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[data-testid='bills-error']");
            Assert.Equal("Unknown InvalidOperationException", alert.TextContent.Trim());
        });
    }

    /// <summary>
    /// Registers a single household and its bills as the API responses, so the page's two-step load
    /// has a deterministic result.
    /// </summary>
    /// <param name="bills">The household's bills.</param>
    /// <returns>The household the bills were registered against.</returns>
    private Household ArrangeHouseholdWith(params Bill[] bills)
    {
        var household = new Household { Id = Guid.NewGuid(), Name = "Family" };
        _apiClient.GetResponsesByPath[BillsPage.HouseholdsPath] = new List<Household> { household };
        _apiClient.GetResponsesByPath[BillsPage.BillsPathFor(household.Id)] = bills.ToList();

        return household;
    }

    /// <summary>Creates a bill with representative values for a test.</summary>
    /// <param name="name">The bill's label.</param>
    /// <param name="value">The bill's amount.</param>
    /// <param name="recurring">Whether the bill repeats.</param>
    /// <param name="interval">The cadence, or <see langword="null"/> for a one-off bill.</param>
    /// <returns>The bill.</returns>
    private static Bill SampleBill(
        string name,
        double value,
        bool recurring = false,
        Interval? interval = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Value = value,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            Recurring = recurring,
            Interval = interval,
            HouseholdId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };
}

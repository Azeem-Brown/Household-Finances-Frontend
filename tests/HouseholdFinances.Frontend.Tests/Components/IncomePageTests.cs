using Bunit;
using HouseholdFinances.Frontend.Models;
using HouseholdFinances.Frontend.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using IncomePage = HouseholdFinances.Frontend.Components.Pages.Income;

namespace HouseholdFinances.Frontend.Tests.Components;

/// <summary>
/// bUnit component tests for the Income page (issue #13): the populated state renders each entry's
/// label, value, dates, and recurring state; the summary equals the sum of the listed entries; and
/// the empty state renders a call to action instead of a bare table. Load failures surface through
/// the shared client error convention.
/// </summary>
/// <remarks>
/// The API client is replaced with a test double, so no live backend or browser is involved. The
/// tests prove the page resolves the current household, requests that household's income, formats
/// the entries and total with the decided money rules, and shows guidance or an error. They do not
/// prove the real backend returns the entries or that a browser renders the page.
/// </remarks>
public class IncomePageTests : BunitContext
{
    private readonly StubApiClient _apiClient = new();

    public IncomePageTests()
    {
        // MudBlazor components may touch JS interop; loose mode keeps render side-effect free.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IApiClient>(_apiClient);
    }

    [Fact]
    public void Income_Populated_RendersEachEntryWithItsLabelAndValue()
    {
        ArrangeHouseholdWith(
            SampleIncome("Salary", 1500.00),
            SampleIncome("Freelance", 250.50));

        var cut = Render<IncomePage>();

        cut.WaitForAssertion(() =>
        {
            var names = cut.FindAll("[data-testid='income-name']")
                .Select(cell => cell.TextContent.Trim())
                .ToList();
            Assert.Contains("Salary", names);
            Assert.Contains("Freelance", names);

            var values = cut.FindAll("[data-testid='income-value']")
                .Select(cell => cell.TextContent.Trim())
                .ToList();
            Assert.Contains("$1,500.00", values);
            Assert.Contains("$250.50", values);
        });
    }

    [Fact]
    public void Income_Populated_SummaryEqualsTheSumOfTheListedEntries()
    {
        var entries = new[]
        {
            SampleIncome("Salary", 1500.00),
            SampleIncome("Freelance", 250.50),
            SampleIncome("Bonus", 1499.50)
        };
        ArrangeHouseholdWith(entries);

        var cut = Render<IncomePage>();

        // The expected total is derived from the same entries, so the assertion proves the summary
        // is the sum of the listed income, not a hard-coded value.
        var expectedTotal = MoneyFormatter.Format(entries.Sum(entry => entry.Value));

        cut.WaitForAssertion(() =>
            Assert.Equal(expectedTotal, cut.Find("[data-testid='income-total']").TextContent.Trim()));

        Assert.Equal("$3,250.00", expectedTotal);
    }

    [Fact]
    public void Income_Populated_ShowsDatesAndRecurringState()
    {
        ArrangeHouseholdWith(
            SampleIncome("Salary", 1500.00, recurring: true, Interval.Monthly),
            SampleIncome("Tax refund", 400.00, recurring: false, interval: null));

        var cut = Render<IncomePage>();

        cut.WaitForAssertion(() =>
        {
            var starts = cut.FindAll("[data-testid='income-start']")
                .Select(cell => cell.TextContent.Trim())
                .ToList();
            Assert.All(starts, start => Assert.Equal("2026-01-01", start));

            var ends = cut.FindAll("[data-testid='income-end']")
                .Select(cell => cell.TextContent.Trim())
                .ToList();
            Assert.All(ends, end => Assert.Equal("2026-12-31", end));

            var recurrence = cut.FindAll("[data-testid='income-recurring']")
                .Select(cell => cell.TextContent.Trim())
                .ToList();
            Assert.Contains("Recurring (Monthly)", recurrence);
            Assert.Contains("One-off", recurrence);
        });
    }

    [Fact]
    public void Income_Populated_ResolvesTheHouseholdThenLoadsItsIncomes()
    {
        var household = ArrangeHouseholdWith(SampleIncome("Salary", 1500.00));

        var cut = Render<IncomePage>();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='income-table']")));

        // The page resolves the household first, then lists income for that household only.
        Assert.Equal(
            new[] { IncomePage.HouseholdsPath, IncomePage.IncomesPathFor(household.Id) },
            _apiClient.Gets);
    }

    [Fact]
    public void Income_Empty_RendersCallToActionInsteadOfABareTable()
    {
        ArrangeHouseholdWith();

        var cut = Render<IncomePage>();

        cut.WaitForAssertion(() =>
        {
            var empty = cut.Find("[data-testid='income-empty']");
            Assert.Contains(IncomePage.EmptyStateMessage, empty.TextContent);
            Assert.Contains("Add income", cut.Find("[data-testid='income-add']").TextContent);
        });

        // Guidance replaces the table, so no bare empty table is shown.
        Assert.Empty(cut.FindAll("[data-testid='income-table']"));
        Assert.Empty(cut.FindAll("[data-testid='income-total']"));
    }

    [Fact]
    public void Income_NoHousehold_RendersGuidanceAndDoesNotRequestIncomes()
    {
        _apiClient.GetResponsesByPath[IncomePage.HouseholdsPath] = new List<Household>();

        var cut = Render<IncomePage>();

        cut.WaitForAssertion(() =>
        {
            var empty = cut.Find("[data-testid='income-no-household']");
            Assert.Contains(IncomePage.NoHouseholdMessage, empty.TextContent);
        });

        // With no household there is nothing to nest income under, so only the list call is made.
        Assert.Equal(new[] { IncomePage.HouseholdsPath }, _apiClient.Gets);
    }

    [Fact]
    public void Income_HouseholdLoadFailure_ShowsConventionMessageAndNoTable()
    {
        _apiClient.Throw = new ClientErrorException(ClientErrorCode.Unauthorized, 7);

        var cut = Render<IncomePage>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[data-testid='income-error']");
            Assert.Equal("Unauthorized 7", alert.TextContent.Trim());
        });

        Assert.Empty(cut.FindAll("[data-testid='income-table']"));
    }

    [Fact]
    public void Income_NestedIncomesFailure_ShowsConventionMessageAndNoTable()
    {
        var household = new Household { Id = Guid.NewGuid(), Name = "Family" };
        _apiClient.GetResponsesByPath[IncomePage.HouseholdsPath] = new List<Household> { household };

        // The backend reports a household the user does not belong to as not found, which the shared
        // convention turns into "NotFound <id>".
        _apiClient.ThrowByPath[IncomePage.IncomesPathFor(household.Id)] =
            new ClientErrorException(ClientErrorCode.NotFound, household.Id);

        var cut = Render<IncomePage>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[data-testid='income-error']");
            Assert.Equal($"NotFound {household.Id}", alert.TextContent.Trim());
        });

        Assert.Empty(cut.FindAll("[data-testid='income-table']"));
    }

    [Fact]
    public void Income_UnexpectedFailure_ShowsConventionMessage()
    {
        _apiClient.Throw = new InvalidOperationException("boom");

        var cut = Render<IncomePage>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[data-testid='income-error']");
            Assert.Equal("Unknown InvalidOperationException", alert.TextContent.Trim());
        });
    }

    /// <summary>
    /// Registers a single household and its income entries as the API responses, so the page's
    /// two-step load has a deterministic result.
    /// </summary>
    /// <param name="entries">The household's income entries.</param>
    /// <returns>The household the entries were registered against.</returns>
    private Household ArrangeHouseholdWith(params Income[] entries)
    {
        var household = new Household { Id = Guid.NewGuid(), Name = "Family" };
        _apiClient.GetResponsesByPath[IncomePage.HouseholdsPath] = new List<Household> { household };
        _apiClient.GetResponsesByPath[IncomePage.IncomesPathFor(household.Id)] = entries.ToList();

        return household;
    }

    /// <summary>Creates an income entry with representative values for a test.</summary>
    /// <param name="name">The entry's label.</param>
    /// <param name="value">The entry's amount.</param>
    /// <param name="recurring">Whether the entry repeats.</param>
    /// <param name="interval">The cadence, or <see langword="null"/> for a one-off entry.</param>
    /// <returns>The income entry.</returns>
    private static Income SampleIncome(
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
            UserId = Guid.NewGuid()
        };
}

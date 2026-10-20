using System.Text.Json;
using Bunit;
using HouseholdFinances.Frontend.Models;
using HouseholdFinances.Frontend.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
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
public class IncomePageTests : BunitContext, IAsyncLifetime
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly StubApiClient _apiClient = new();

    public IncomePageTests()
    {
        // MudBlazor components may touch JS interop; loose mode keeps render side-effect free.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IApiClient>(_apiClient);
    }

    // The create/edit form renders MudBlazor inputs that resolve services which only implement
    // IAsyncDisposable, so the context must be disposed asynchronously. xUnit routes teardown
    // through IAsyncLifetime instead of the synchronous IDisposable.Dispose when it is implemented.
    public Task InitializeAsync() => Task.CompletedTask;

    async Task IAsyncLifetime.DisposeAsync() => await ((IAsyncDisposable)this).DisposeAsync();

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

    [Fact]
    public void Income_AddForm_IsHiddenUntilAddIsChosenAndStartsWithNoIntervalSelector()
    {
        ArrangeHouseholdWith(SampleIncome("Salary", 1500.00));

        var cut = Render<IncomePage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='income-table']")));

        Assert.Empty(cut.FindAll("[data-testid='income-form']"));

        cut.Find("[data-testid='income-add']").Click();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='income-form']")));

        // A new entry is one-off by default, so the interval selector is not shown.
        Assert.Empty(cut.FindAll("[data-testid='income-interval']"));
    }

    [Fact]
    public void Income_AddForm_EmptyName_ShowsRequiredMessageAndDoesNotCallTheApi()
    {
        ArrangeHouseholdWith();

        var cut = Render<IncomePage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='income-add']")));

        cut.Find("[data-testid='income-add']").Click();
        cut.Find("[data-testid='income-form-submit']").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(IncomePage.NameRequiredMessage, cut.Markup));

        Assert.Empty(_apiClient.Posts);
        Assert.Single(cut.FindAll("[data-testid='income-form']"));
    }

    [Fact]
    public void Income_AddForm_NegativeValue_ShowsMessageAndDoesNotCallTheApi()
    {
        ArrangeHouseholdWith();

        var cut = Render<IncomePage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='income-add']")));

        cut.Find("[data-testid='income-add']").Click();
        cut.Find("[data-testid='income-form-name']").Input("Salary");
        cut.Find("[data-testid='income-form-value']").Change("-5");
        cut.Find("[data-testid='income-form-submit']").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(IncomePage.ValueNegativeMessage, cut.Markup));

        Assert.Empty(_apiClient.Posts);
    }

    [Fact]
    public void Income_AddForm_EndDateBeforeStart_ShowsMessageAndDoesNotCallTheApi()
    {
        ArrangeHouseholdWith();

        var cut = Render<IncomePage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='income-add']")));

        cut.Find("[data-testid='income-add']").Click();
        cut.Find("[data-testid='income-form-name']").Input("Salary");

        // The start date defaults to today, so a far-past end date is always before it.
        cut.Find("[data-testid='income-form-end']").Change("2000-01-01");
        cut.Find("[data-testid='income-form-submit']").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(IncomePage.EndDateBeforeStartMessage, cut.Markup));

        Assert.Empty(_apiClient.Posts);
    }

    [Fact]
    public void Income_IntervalSelector_IsShownAndRequiredOnlyWhenRecurring()
    {
        ArrangeHouseholdWith();

        var cut = Render<IncomePage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='income-add']")));

        cut.Find("[data-testid='income-add']").Click();
        Assert.Empty(cut.FindAll("[data-testid='income-interval']"));

        // Turning recurring on reveals the interval selector.
        cut.Find("[data-testid='income-recurring-toggle']").Change(true);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='income-interval']")));

        // A recurring entry with no interval is rejected before any request is sent.
        cut.Find("[data-testid='income-form-name']").Input("Salary");
        cut.Find("[data-testid='income-form-submit']").Click();
        cut.WaitForAssertion(() =>
            Assert.Contains(IncomePage.IntervalRequiredMessage, cut.Markup));
        Assert.Empty(_apiClient.Posts);

        // Turning recurring back off hides the selector again.
        cut.Find("[data-testid='income-recurring-toggle']").Change(false);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='income-interval']")));
    }

    [Fact]
    public void Income_AddForm_ValidEntry_PostsAndRefreshesTheList()
    {
        var household = ArrangeHouseholdWith();
        var incomesPath = IncomePage.IncomesPathFor(household.Id);

        var cut = Render<IncomePage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='income-add']")));

        cut.Find("[data-testid='income-add']").Click();
        cut.Find("[data-testid='income-form-name']").Input("Salary");
        cut.Find("[data-testid='income-form-value']").Change("1500");

        // The save reloads the list from the backend, so make the backend return the new entry.
        _apiClient.GetResponsesByPath[incomesPath] = new List<Income> { SampleIncome("Salary", 1500.00) };

        cut.Find("[data-testid='income-form-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("$1,500.00", cut.Find("[data-testid='income-total']").TextContent.Trim());
            Assert.Contains(
                "Salary",
                cut.FindAll("[data-testid='income-name']").Select(cell => cell.TextContent.Trim()));
        });

        var post = Assert.Single(_apiClient.Posts);
        Assert.Equal(incomesPath, post.Path);

        using var body = JsonDocument.Parse(JsonSerializer.Serialize(post.Body, WebJson));
        Assert.Equal("Salary", body.RootElement.GetProperty("name").GetString());
        Assert.Equal(1500d, body.RootElement.GetProperty("value").GetDouble());
        Assert.False(body.RootElement.GetProperty("recurring").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("interval").ValueKind);

        // The list was re-requested after the save, so the total reflects the change.
        Assert.Equal(2, _apiClient.Gets.Count(path => path == incomesPath));
    }

    [Fact]
    public void Income_EditForm_IsPrefilledAndPutsTheChange()
    {
        var entry = SampleIncome("Salary", 1500.00);
        var household = ArrangeHouseholdWith(entry);

        var cut = Render<IncomePage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='income-edit']")));

        cut.Find("[data-testid='income-edit']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Single(cut.FindAll("[data-testid='income-form']"));
            Assert.Equal(
                "Salary",
                cut.Find("[data-testid='income-form-name']").GetAttribute("value"));
        });

        cut.Find("[data-testid='income-form-name']").Input("Bonus");
        cut.Find("[data-testid='income-form-submit']").Click();

        cut.WaitForAssertion(() => Assert.Single(_apiClient.Puts));

        var put = Assert.Single(_apiClient.Puts);
        Assert.Equal(IncomePage.IncomePathFor(household.Id, entry.Id), put.Path);

        using var body = JsonDocument.Parse(JsonSerializer.Serialize(put.Body, WebJson));
        Assert.Equal("Bonus", body.RootElement.GetProperty("name").GetString());

        // No create was issued for an edit.
        Assert.Empty(_apiClient.Posts);
    }

    [Fact]
    public void Income_Delete_RemovesTheEntryAndRefreshesTheList()
    {
        var entry = SampleIncome("Salary", 1500.00);
        var household = ArrangeHouseholdWith(entry);
        var incomesPath = IncomePage.IncomesPathFor(household.Id);

        var cut = Render<IncomePage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='income-delete']")));

        // The delete reloads the list, so make the backend return nothing afterwards.
        _apiClient.GetResponsesByPath[incomesPath] = new List<Income>();

        cut.Find("[data-testid='income-delete']").Click();

        cut.WaitForAssertion(() =>
            Assert.Contains(IncomePage.EmptyStateMessage, cut.Markup));

        Assert.Equal(IncomePage.IncomePathFor(household.Id, entry.Id), Assert.Single(_apiClient.Deletes));
        Assert.Equal(2, _apiClient.Gets.Count(path => path == incomesPath));
    }

    [Fact]
    public void Income_SaveFailure_ShowsConventionMessageAndKeepsTheFormOpen()
    {
        ArrangeHouseholdWith();

        var cut = Render<IncomePage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='income-add']")));

        cut.Find("[data-testid='income-add']").Click();
        cut.Find("[data-testid='income-form-name']").Input("Salary");

        _apiClient.Throw = new ClientErrorException(ClientErrorCode.InvalidInput, "Name");

        cut.Find("[data-testid='income-form-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[data-testid='income-operation-error']");
            Assert.Equal("InvalidInput Name", alert.TextContent.Trim());
        });

        // The form stays open so the user can correct the input and retry.
        Assert.Single(cut.FindAll("[data-testid='income-form']"));
    }

    [Fact]
    public async Task Income_AddForm_RecurringEntry_OffersEveryIntervalAndSendsTheChosenOne()
    {
        ArrangeHouseholdWith();

        // MudBlazor renders the options of a MudSelect inside MudPopoverProvider. The layout supplies
        // it in the running app, so it has to be rendered explicitly here for options to exist at all.
        var popover = Render<MudPopoverProvider>();

        var cut = Render<IncomePage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='income-add']")));

        cut.Find("[data-testid='income-add']").Click();
        cut.Find("[data-testid='income-form-name']").Input("Salary");
        cut.Find("[data-testid='income-recurring-toggle']").Change(true);
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<MudSelect<Interval?>>()));

        var select = cut.FindComponent<MudSelect<Interval?>>();
        await cut.InvokeAsync(() => select.Instance.OpenMenu());

        // Every supported cadence is offered, so any of them can be set on a recurring entry.
        popover.WaitForAssertion(() =>
        {
            var labels = popover.FindAll("div.mud-list-item")
                .Select(item => item.TextContent.Trim())
                .ToList();
            Assert.Equal(
                new[] { "Daily", "Weekly", "BiWeekly", "Monthly", "Quarterly", "Yearly" },
                labels);
        });

        popover.FindAll("div.mud-list-item")
            .First(item => item.TextContent.Trim() == "Yearly")
            .Click();

        cut.Find("[data-testid='income-form-submit']").Click();
        cut.WaitForAssertion(() => Assert.Single(_apiClient.Posts));

        using var body = JsonDocument.Parse(
            JsonSerializer.Serialize(_apiClient.Posts[0].Body, WebJson));
        Assert.True(body.RootElement.GetProperty("recurring").GetBoolean());
        // The contract serializes enums as their numeric values.
        Assert.Equal((int)Interval.Yearly, body.RootElement.GetProperty("interval").GetInt32());
    }

    [Fact]
    public void Income_EditForm_RecurringEntry_RestoresTheSavedIntervalAndPutsItBack()
    {
        var entry = SampleIncome("Rent", 1000.00, recurring: true, interval: Interval.Quarterly);
        var household = ArrangeHouseholdWith(entry);

        var cut = Render<IncomePage>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='income-edit']")));

        cut.Find("[data-testid='income-edit']").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<MudSelect<Interval?>>()));

        // The saved cadence is restored into the selector rather than resetting to a default.
        cut.WaitForAssertion(() => Assert.Contains(
            Interval.Quarterly.ToString(),
            cut.FindComponent<MudSelect<Interval?>>().Markup));

        cut.Find("[data-testid='income-form-submit']").Click();
        cut.WaitForAssertion(() => Assert.Single(_apiClient.Puts));

        var put = Assert.Single(_apiClient.Puts);
        Assert.Equal(IncomePage.IncomePathFor(household.Id, entry.Id), put.Path);

        // Editing without touching the interval resends the saved cadence instead of dropping it.
        using var body = JsonDocument.Parse(JsonSerializer.Serialize(put.Body, WebJson));
        Assert.True(body.RootElement.GetProperty("recurring").GetBoolean());
        // The contract serializes enums as their numeric values.
        Assert.Equal((int)Interval.Quarterly, body.RootElement.GetProperty("interval").GetInt32());
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

using HouseholdFinances.Frontend.Models;
using HouseholdFinances.Frontend.Services;

namespace HouseholdFinances.Frontend.Tests.Services;

/// <summary>
/// Tests for the recurrence rule recorded on issue #5 and restated by issue #47: occurrences are
/// expanded from the start date, stepping by the interval until the end date inclusive, and a
/// window total is the sum of the occurrences that fall inside an inclusive calendar window.
/// </summary>
public class OccurrenceCalculatorTests
{
    // The accepted fixture pins "now" to a Wednesday so the week anchors are observable.
    private static readonly DateOnly Today = new(2026, 10, 14);

    [Theory]
    [InlineData(Interval.Daily, "2026-10-14", "2026-10-14")]
    [InlineData(Interval.Weekly, "2026-10-12", "2026-10-18")]
    [InlineData(Interval.BiWeekly, "2026-10-12", "2026-10-25")]
    [InlineData(Interval.Monthly, "2026-10-01", "2026-10-31")]
    [InlineData(Interval.Quarterly, "2026-10-01", "2026-12-31")]
    [InlineData(Interval.Yearly, "2026-01-01", "2026-12-31")]
    public void WindowFor_ReturnsTheInclusiveCalendarWindowContainingToday(
        Interval period,
        string expectedStart,
        string expectedEnd)
    {
        var window = OccurrenceCalculator.WindowFor(period, Today);

        Assert.Equal(DateOnly.Parse(expectedStart), window.Start);
        Assert.Equal(DateOnly.Parse(expectedEnd), window.End);
    }

    [Theory]
    [InlineData(Interval.Weekly, "2026-10-12", "2026-10-18")]
    [InlineData(Interval.BiWeekly, "2026-10-12", "2026-10-25")]
    public void WindowFor_AnchorsWeeksToTheMondayOfTheCurrentWeekEvenOnASunday(
        Interval period,
        string expectedStart,
        string expectedEnd)
    {
        // 2026-10-18 is a Sunday, which must still belong to the week that began on Monday.
        var window = OccurrenceCalculator.WindowFor(period, new DateOnly(2026, 10, 18));

        Assert.Equal(DateOnly.Parse(expectedStart), window.Start);
        Assert.Equal(DateOnly.Parse(expectedEnd), window.End);
    }

    [Theory]
    [InlineData(Interval.Daily, 0.00)]
    [InlineData(Interval.Weekly, 100.00)]
    [InlineData(Interval.BiWeekly, 300.00)]
    [InlineData(Interval.Monthly, 400.00)]
    [InlineData(Interval.Quarterly, 1200.00)]
    [InlineData(Interval.Yearly, 5120.00)]
    public void Totals_MatchTheAcceptedFiguresForEveryPeriod(Interval period, double expectedCombined)
    {
        var totals = TotalsFor(period, B1, B2, B3);

        // Every fixture entry recurs, so the combined figure is entirely recurring.
        Assert.Equal(expectedCombined, totals.Combined);
        Assert.Equal(expectedCombined, totals.Recurring);
        Assert.Equal(0, totals.OneOff);
    }

    [Theory]
    [InlineData(Interval.Monthly, 900.00, 400.00, 500.00)]
    [InlineData(Interval.Weekly, 600.00, 100.00, 500.00)]
    public void OneOff_IsCountedOnceAndIsNeverScaledByThePeriod(
        Interval period,
        double expectedCombined,
        double expectedRecurring,
        double expectedOneOff)
    {
        var totals = TotalsFor(period, B1, B2, B3, B4);

        Assert.Equal(expectedCombined, totals.Combined);
        Assert.Equal(expectedRecurring, totals.Recurring);
        Assert.Equal(expectedOneOff, totals.OneOff);
    }

    [Theory]
    [InlineData(Interval.Monthly, 450.00)]
    [InlineData(Interval.Quarterly, 1250.00)]
    [InlineData(Interval.Yearly, 5620.00)]
    public void EndDate_StopsTheSeriesInsideTheWindow(Interval period, double expectedCombined)
    {
        var totals = TotalsFor(period, B1, B2, B3, B5);

        Assert.Equal(expectedCombined, totals.Combined);
    }

    [Fact]
    public void EndDate_InsideTheWindow_LimitsOccurrencesToTheSeriesEnd()
    {
        var window = OccurrenceCalculator.WindowFor(Interval.Quarterly, Today);

        // B5 runs monthly from January to 20 October, so only its October occurrence is in Q4.
        var occurrences = OccurrenceCalculator.OccurrencesInWindow(B5, window);

        Assert.Equal(new[] { new DateOnly(2026, 10, 20) }, occurrences);
    }

    [Fact]
    public void Occurrences_AnchorCalendarStepsToTheOriginalStartDate()
    {
        // A series starting on 31 January must clamp to the end of February and then return to the
        // 31st, rather than accumulating the clamp and stepping from 28 March.
        var entry = new MoneyEntry(
            10,
            new DateTime(2026, 1, 31),
            new DateTime(2026, 4, 30),
            Recurring: true,
            Interval.Monthly);

        var occurrences = OccurrenceCalculator.Occurrences(entry);

        Assert.Equal(
            new[]
            {
                new DateOnly(2026, 1, 31),
                new DateOnly(2026, 2, 28),
                new DateOnly(2026, 3, 31),
                new DateOnly(2026, 4, 30)
            },
            occurrences);
    }

    [Fact]
    public void WindowBounds_AreInclusiveAtBothEnds()
    {
        var window = OccurrenceCalculator.WindowFor(Interval.Monthly, Today);
        var entry = new MoneyEntry(
            1,
            new DateTime(2026, 10, 1),
            new DateTime(2026, 10, 31),
            Recurring: true,
            Interval.Daily);

        var occurrences = OccurrenceCalculator.OccurrencesInWindow(entry, window);

        Assert.Equal(31, occurrences.Count);
        Assert.Equal(new DateOnly(2026, 10, 1), occurrences[0]);
        Assert.Equal(new DateOnly(2026, 10, 31), occurrences[^1]);
    }

    [Fact]
    public void OccurrenceExactlyOnEndDate_Counts()
    {
        var window = OccurrenceCalculator.WindowFor(Interval.Monthly, Today);
        var entry = new MoneyEntry(
            50,
            new DateTime(2026, 9, 20),
            new DateTime(2026, 10, 20),
            Recurring: true,
            Interval.Monthly);

        var totals = OccurrenceCalculator.Totals(new[] { entry }, window);

        Assert.Equal(50, totals.Combined);
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, Interval.Monthly)]
    [InlineData(false, null)]
    public void OneOffClassification_CountsOnceWhenTheStartDateIsInsideTheWindow(
        bool recurring,
        Interval? interval)
    {
        // Recurring with a null interval must not be read as Daily; an interval without the
        // recurring flag is still a one-off.
        var entry = new MoneyEntry(
            30,
            new DateTime(2026, 10, 14),
            new DateTime(2026, 10, 14),
            recurring,
            interval);

        var totals = TotalsFor(Interval.Daily, entry);

        Assert.Equal(30, totals.Combined);
        Assert.Equal(0, totals.Recurring);
        Assert.Equal(30, totals.OneOff);
    }

    [Theory]
    [InlineData(Interval.Daily, 1)]
    [InlineData(Interval.Monthly, 1)]
    [InlineData(Interval.Yearly, 1)]
    public void OneOff_IsNotScaledByTheSelectedPeriod(Interval period, int expectedOccurrences)
    {
        var entry = new MoneyEntry(
            500,
            new DateTime(2026, 10, 14),
            new DateTime(2026, 10, 14),
            Recurring: false,
            Interval: null);

        var occurrences = OccurrenceCalculator.OccurrencesInWindow(
            entry,
            OccurrenceCalculator.WindowFor(period, Today));

        Assert.Equal(expectedOccurrences, occurrences.Count);
    }

    [Fact]
    public void OneOff_OutsideTheWindow_ContributesNothing()
    {
        var entry = new MoneyEntry(
            500,
            new DateTime(2026, 1, 1),
            new DateTime(2026, 1, 1),
            Recurring: false,
            Interval: null);

        var totals = TotalsFor(Interval.Monthly, entry);

        Assert.Equal(0, totals.Combined);
        Assert.Equal(0, totals.OneOff);
    }

    [Theory]
    [InlineData(true, Interval.Monthly)]
    [InlineData(false, null)]
    public void EndDateBeforeStartDate_ProducesNothingAndDoesNotThrow(bool recurring, Interval? interval)
    {
        // The start date is inside the window, but the invalid range must not contribute.
        var entry = new MoneyEntry(
            500,
            new DateTime(2026, 10, 14),
            new DateTime(2026, 10, 1),
            recurring,
            interval);

        var window = OccurrenceCalculator.WindowFor(Interval.Monthly, Today);

        Assert.Empty(OccurrenceCalculator.Occurrences(entry));
        Assert.Empty(OccurrenceCalculator.OccurrencesInWindow(entry, window));
        Assert.Equal(0, OccurrenceCalculator.Totals(new[] { entry }, window).Combined);
    }

    [Fact]
    public void Totals_AreUnroundedSoDisplayRoundingHappensOnce()
    {
        // Three one-offs of one third each must total 1.00 before display rounds it.
        var entry = new MoneyEntry(
            1d / 3d,
            new DateTime(2026, 10, 14),
            new DateTime(2026, 10, 14),
            Recurring: false,
            Interval: null);

        var totals = TotalsFor(Interval.Monthly, entry, entry, entry);

        Assert.Equal(1d, totals.Combined, precision: 12);
        Assert.Equal("$1.00", MoneyFormatter.Format(totals.Combined));
    }

    private static OccurrenceTotals TotalsFor(Interval period, params MoneyEntry[] entries) =>
        OccurrenceCalculator.Totals(
            entries,
            OccurrenceCalculator.WindowFor(period, Today));

    // The accepted bills fixture from issue #47.
    private static MoneyEntry B1 => new(
        100,
        new DateTime(2026, 1, 5),
        new DateTime(2026, 12, 31),
        Recurring: true,
        Interval.BiWeekly);

    private static MoneyEntry B2 => new(
        200,
        new DateTime(2026, 1, 20),
        new DateTime(2026, 12, 31),
        Recurring: true,
        Interval.Monthly);

    private static MoneyEntry B3 => new(
        120,
        new DateTime(2026, 2, 1),
        new DateTime(2026, 12, 31),
        Recurring: true,
        Interval.Yearly);

    private static MoneyEntry B4 => new(
        500,
        new DateTime(2026, 10, 14),
        new DateTime(2026, 10, 14),
        Recurring: false,
        Interval: null);

    private static MoneyEntry B5 => new(
        50,
        new DateTime(2026, 1, 20),
        new DateTime(2026, 10, 20),
        Recurring: true,
        Interval.Monthly);
}

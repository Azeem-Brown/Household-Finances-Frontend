using HouseholdFinances.Frontend.Models;
using HouseholdFinances.Frontend.Services;

namespace HouseholdFinances.Frontend.Tests.Services;

/// <summary>
/// Verifies that goals expand into occurrence dates with the same rule and calendar windows as
/// income and bills, using the pinned fixture from issue #49.
/// </summary>
public class GoalOccurrencesTests
{
    private static readonly DateOnly Today = new(2026, 10, 14);

    private static readonly Goal MonthlyGoal = new()
    {
        Name = "G1",
        Value = 10000.00,
        StartDate = new DateTime(2026, 1, 20),
        EndDate = new DateTime(2026, 12, 31),
        Recurring = true,
        Interval = Interval.Monthly
    };

    private static readonly Goal BiWeeklyGoal = new()
    {
        Name = "G2",
        Value = 5000.00,
        StartDate = new DateTime(2026, 1, 5),
        EndDate = new DateTime(2026, 12, 31),
        Recurring = true,
        Interval = Interval.BiWeekly
    };

    [Theory]
    [InlineData(Interval.Daily, "")]
    [InlineData(Interval.Weekly, "2026-10-12")]
    [InlineData(Interval.BiWeekly, "2026-10-12,2026-10-20")]
    [InlineData(Interval.Monthly, "2026-10-12,2026-10-20,2026-10-26")]
    [InlineData(
        Interval.Quarterly,
        "2026-10-12,2026-10-20,2026-10-26,2026-11-09,2026-11-20,2026-11-23,2026-12-07,2026-12-20,2026-12-21")]
    public void InWindow_MatchesPinnedOccurrencesForEachWindow(Interval period, string expectedCsv)
    {
        var window = OccurrenceCalculator.WindowFor(period, Today);
        var expected = expectedCsv.Length == 0
            ? Array.Empty<DateOnly>()
            : expectedCsv.Split(',').Select(DateOnly.Parse).ToArray();

        Assert.Equal(expected, Combined(window));
    }

    [Fact]
    public void InWindow_YearlyWindowHasTwelveMonthlyPlusTwentySixBiWeeklyOccurrences()
    {
        var window = OccurrenceCalculator.WindowFor(Interval.Yearly, Today);

        var monthly = GoalOccurrences.InWindow(MonthlyGoal, window);
        var biWeekly = GoalOccurrences.InWindow(BiWeeklyGoal, window);

        Assert.Equal(12, monthly.Count);
        Assert.Equal(26, biWeekly.Count);
        Assert.Equal(38, Combined(window).Length);
        Assert.Equal(new DateOnly(2026, 1, 20), monthly[0]);
        Assert.Equal(new DateOnly(2026, 12, 20), monthly[^1]);
        Assert.Equal(new DateOnly(2026, 1, 5), biWeekly[0]);
        Assert.Equal(new DateOnly(2026, 12, 21), biWeekly[^1]);
    }

    [Theory]
    [InlineData(Interval.Daily)]
    [InlineData(Interval.Weekly)]
    [InlineData(Interval.BiWeekly)]
    [InlineData(Interval.Monthly)]
    [InlineData(Interval.Quarterly)]
    [InlineData(Interval.Yearly)]
    public void InWindow_OneOffGoalHasSingleOccurrenceWhenStartDateIsInsideWindow(Interval period)
    {
        var goal = new Goal
        {
            Name = "New bike",
            Value = 500.00,
            StartDate = new DateTime(2026, 10, 14),
            EndDate = new DateTime(2026, 10, 14),
            Recurring = false,
            Interval = null
        };

        var window = OccurrenceCalculator.WindowFor(period, Today);

        Assert.Equal(new[] { new DateOnly(2026, 10, 14) }, GoalOccurrences.InWindow(goal, window));
    }

    [Fact]
    public void InWindow_RecurringWithNullIntervalIsOneOffAndNeverDaily()
    {
        var goal = new Goal
        {
            Name = "Recurring but cadence-less",
            Value = 500.00,
            StartDate = new DateTime(2026, 10, 14),
            EndDate = new DateTime(2026, 12, 31),
            Recurring = true,
            Interval = null
        };

        // The yearly window would contain hundreds of dates if a null interval read as Daily.
        var window = OccurrenceCalculator.WindowFor(Interval.Yearly, Today);

        Assert.Equal(new[] { new DateOnly(2026, 10, 14) }, GoalOccurrences.InWindow(goal, window));
    }

    [Fact]
    public void InWindow_NonRecurringGoalWithIntervalIsOneOffOnStartDate()
    {
        var goal = new Goal
        {
            Name = "Target date",
            Value = 500.00,
            StartDate = new DateTime(2026, 10, 5),
            EndDate = new DateTime(2026, 12, 31),
            Recurring = false,
            Interval = Interval.Monthly
        };

        var window = OccurrenceCalculator.WindowFor(Interval.Yearly, Today);

        Assert.Equal(new[] { new DateOnly(2026, 10, 5) }, GoalOccurrences.InWindow(goal, window));
    }

    [Fact]
    public void InWindow_IncludesOccurrenceExactlyOnEndDate()
    {
        var goal = new Goal
        {
            Name = "G1 ending in October",
            Value = 10000.00,
            StartDate = new DateTime(2026, 1, 20),
            EndDate = new DateTime(2026, 10, 20),
            Recurring = true,
            Interval = Interval.Monthly
        };

        var window = OccurrenceCalculator.WindowFor(Interval.Monthly, Today);
        var occurrences = GoalOccurrences.InWindow(goal, window);

        Assert.Equal(new[] { new DateOnly(2026, 10, 20) }, occurrences);
    }

    [Fact]
    public void InWindow_EndDateBeforeStartDateReturnsEmptyWithoutThrowing()
    {
        var goal = new Goal
        {
            Name = "Invalid range",
            Value = 1000.00,
            StartDate = new DateTime(2026, 10, 20),
            EndDate = new DateTime(2026, 10, 1),
            Recurring = true,
            Interval = Interval.Monthly
        };

        var window = OccurrenceCalculator.WindowFor(Interval.Yearly, Today);

        Assert.Empty(GoalOccurrences.InWindow(goal, window));
    }

    [Fact]
    public void InWindow_ValueDoesNotAffectOccurrenceDates()
    {
        var lowValueGoal = new Goal
        {
            Name = "G1",
            Value = 1.00,
            StartDate = new DateTime(2026, 1, 20),
            EndDate = new DateTime(2026, 12, 31),
            Recurring = true,
            Interval = Interval.Monthly
        };

        var highValueGoal = new Goal
        {
            Name = "G1",
            Value = 1000000.00,
            StartDate = new DateTime(2026, 1, 20),
            EndDate = new DateTime(2026, 12, 31),
            Recurring = true,
            Interval = Interval.Monthly
        };

        var window = OccurrenceCalculator.WindowFor(Interval.Yearly, Today);

        Assert.Equal(
            GoalOccurrences.InWindow(lowValueGoal, window),
            GoalOccurrences.InWindow(highValueGoal, window));
    }

    [Fact]
    public void InWindow_AnchorsMonthlySteppingToOriginalStartDate()
    {
        var goal = new Goal
        {
            Name = "End of month",
            Value = 1000.00,
            StartDate = new DateTime(2026, 1, 31),
            EndDate = new DateTime(2026, 3, 31),
            Recurring = true,
            Interval = Interval.Monthly
        };

        var window = OccurrenceCalculator.WindowFor(Interval.Yearly, Today);

        Assert.Equal(
            new[] { new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31) },
            GoalOccurrences.InWindow(goal, window));
    }

    private static DateOnly[] Combined(CalendarWindow window) =>
        GoalOccurrences.InWindow(MonthlyGoal, window)
            .Concat(GoalOccurrences.InWindow(BiWeeklyGoal, window))
            .OrderBy(date => date)
            .ToArray();
}

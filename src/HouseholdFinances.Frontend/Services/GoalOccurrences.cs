using HouseholdFinances.Frontend.Models;

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// Expands a <see cref="Goal"/> into the occurrence dates that fall inside an inclusive calendar
/// window, reusing <see cref="OccurrenceCalculator"/> so goals follow the same recurrence rule and
/// windows as income and bills.
/// <para>
/// A goal occurrence is a schedule checkpoint, not a monetary charge. <see cref="Goal.Value"/> is
/// the goal's target amount and is never multiplied by the occurrence count, and
/// <see cref="Goal.Total"/> is the independently stored contributed amount. Goal occurrences also
/// never contribute to household <c>incomes</c> or <c>payments</c>.
/// </para>
/// </summary>
public static class GoalOccurrences
{
    /// <summary>
    /// Lists the goal's schedule checkpoints inside <paramref name="window"/>. A goal is recurring
    /// only when <see cref="Goal.Recurring"/> is true and <see cref="Goal.Interval"/> is non-null;
    /// every other goal is a one-off with at most one occurrence on <see cref="Goal.StartDate"/>.
    /// Dates are compared as <see cref="DateOnly"/>, so time of day never affects the result, and
    /// an <see cref="Goal.EndDate"/> before <see cref="Goal.StartDate"/> yields an empty result.
    /// </summary>
    /// <param name="goal">The goal to expand.</param>
    /// <param name="window">The inclusive calendar window.</param>
    /// <returns>The occurrence dates inside the window, in ascending date order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="goal"/> is null.</exception>
    public static IReadOnlyList<DateOnly> InWindow(Goal goal, CalendarWindow window)
    {
        ArgumentNullException.ThrowIfNull(goal);

        // OccurrenceCalculator owns the one-off classification, calendar stepping, and window
        // bounds. Value is carried through but never read by the date expansion.
        return OccurrenceCalculator.OccurrencesInWindow(
            new MoneyEntry(
                goal.Value,
                goal.StartDate,
                goal.EndDate,
                goal.Recurring,
                goal.Interval),
            window);
    }
}

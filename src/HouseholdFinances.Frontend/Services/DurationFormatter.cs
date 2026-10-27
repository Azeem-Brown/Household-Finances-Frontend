using HouseholdFinances.Frontend.Models;

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// Formats a whole number of selected periods as a human-readable duration. Durations are converted
/// exactly to days, weeks, or months; the formatter never invents an approximate weeks-per-month or
/// days-per-month factor, so the displayed value matches the dashboard's selected period.
/// </summary>
public static class DurationFormatter
{
    /// <summary>
    /// Formats <paramref name="periods"/> selected periods, for example <c>3 months</c> for three
    /// monthly periods or <c>6 weeks</c> for three bi-weekly periods.
    /// </summary>
    /// <param name="period">The cadence of each period.</param>
    /// <param name="periods">The number of periods, which must be at least one.</param>
    /// <returns>The formatted duration, with a singular unit for exactly one unit.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="periods"/> is not positive, or <paramref name="period"/> is not one of the
    /// six supported intervals.
    /// </exception>
    public static string Format(Interval period, int periods)
    {
        if (periods <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(periods),
                periods,
                "A duration needs at least one period.");
        }

        return period switch
        {
            Interval.Daily => Quantity(periods, "day"),
            Interval.Weekly => Quantity(periods, "week"),
            Interval.BiWeekly => Quantity(periods * 2, "week"),
            Interval.Monthly => Quantity(periods, "month"),
            Interval.Quarterly => Quantity(periods * 3, "month"),
            Interval.Yearly => Quantity(periods * 12, "month"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(period),
                period,
                "Unsupported interval.")
        };
    }

    private static string Quantity(int value, string unit) =>
        $"{value} {unit}{(value == 1 ? string.Empty : "s")}";
}

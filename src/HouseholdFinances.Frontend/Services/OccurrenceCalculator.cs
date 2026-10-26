using HouseholdFinances.Frontend.Models;

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// An inclusive calendar window, with both <see cref="Start"/> and <see cref="End"/> counted. The
/// window is expressed in calendar dates only, so time of day never affects membership.
/// </summary>
/// <param name="Start">The first date inside the window.</param>
/// <param name="End">The last date inside the window.</param>
public readonly record struct CalendarWindow(DateOnly Start, DateOnly End)
{
    /// <summary>
    /// Determines whether <paramref name="date"/> falls inside the window. Both bounds are
    /// inclusive, so a date exactly on <see cref="Start"/> or <see cref="End"/> is contained.
    /// </summary>
    /// <param name="date">The calendar date to test.</param>
    /// <returns><see langword="true"/> when the date is inside the window.</returns>
    public bool Contains(DateOnly date) => date >= Start && date <= End;
}

/// <summary>
/// The facts a windowed total depends on, expressed independently of <c>Bill</c> and
/// <c>Income</c> so one calculator serves both pages.
/// </summary>
/// <param name="Value">The entered amount, counted once per occurrence.</param>
/// <param name="StartDate">The date the entry starts; ignored before it.</param>
/// <param name="EndDate">The last date an occurrence may fall on, inclusive.</param>
/// <param name="Recurring">Whether the entry repeats; an interval is also required.</param>
/// <param name="Interval">The cadence, or <see langword="null"/> for a one-off entry.</param>
public readonly record struct MoneyEntry(
    double Value,
    DateTime StartDate,
    DateTime EndDate,
    bool Recurring,
    Interval? Interval);

/// <summary>
/// Unrounded totals for one calendar window. Values are not rounded here; display rounds each
/// figure once through <see cref="MoneyFormatter"/>.
/// </summary>
/// <param name="Recurring">The sum of recurring occurrences inside the window.</param>
/// <param name="OneOff">The sum of one-off entries that start inside the window.</param>
public readonly record struct OccurrenceTotals(double Recurring, double OneOff)
{
    /// <summary>
    /// The sum of the two unrounded parts. Both are real amounts inside the window, so the
    /// combined figure is their total.
    /// </summary>
    public double Combined => Recurring + OneOff;
}

/// <summary>
/// Expands recurring entries into discrete occurrences and totals those that fall inside an
/// inclusive calendar window, following the recurrence rule recorded on issue #5. It has no
/// Blazor or MudBlazor dependencies and no clock of its own: callers pass the calendar date to
/// <see cref="WindowFor"/>.
/// </summary>
public static class OccurrenceCalculator
{
    /// <summary>
    /// Builds the calendar window for a selected period, given the current UTC calendar date.
    /// </summary>
    /// <param name="period">The selected period.</param>
    /// <param name="today">The current calendar date, in UTC.</param>
    /// <returns>The inclusive window for the period that contains <paramref name="today"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="period"/> is not one of the six supported intervals.
    /// </exception>
    public static CalendarWindow WindowFor(Interval period, DateOnly today) => period switch
    {
        Interval.Daily => new CalendarWindow(today, today),
        // The week always starts on Monday, so both weekly windows are anchored to it.
        Interval.Weekly => WeekFrom(StartOfWeek(today), weeks: 1),
        Interval.BiWeekly => WeekFrom(StartOfWeek(today), weeks: 2),
        Interval.Monthly => MonthWindow(today.Year, today.Month),
        Interval.Quarterly => QuarterWindow(today),
        Interval.Yearly => new CalendarWindow(
            new DateOnly(today.Year, 1, 1),
            new DateOnly(today.Year, 12, 31)),
        _ => throw new ArgumentOutOfRangeException(
            nameof(period),
            period,
            "Unsupported interval.")
    };

    /// <summary>
    /// Determines whether an entry recurs. An entry is recurring only when
    /// <see cref="MoneyEntry.Recurring"/> is true and <see cref="MoneyEntry.Interval"/> has a
    /// value; a null interval is never read as Daily.
    /// </summary>
    /// <param name="entry">The entry to classify.</param>
    /// <returns><see langword="true"/> when the entry repeats.</returns>
    public static bool IsRecurring(MoneyEntry entry) => entry.Recurring && entry.Interval is not null;

    /// <summary>
    /// Lists every occurrence an entry produces within its own dates. A recurring entry steps by
    /// its interval from the original start date until <see cref="MoneyEntry.EndDate"/> inclusive.
    /// A one-off entry produces its start date once, provided its end date is not before it.
    /// </summary>
    /// <param name="entry">The entry to expand.</param>
    /// <returns>The occurrences in ascending date order, bounded by the entry's own dates.</returns>
    public static IReadOnlyList<DateOnly> Occurrences(MoneyEntry entry)
    {
        DateOnly start = ToDate(entry.StartDate);
        DateOnly end = ToDate(entry.EndDate);

        if (end < start)
        {
            return Array.Empty<DateOnly>();
        }

        return IsRecurring(entry)
            ? RecurringOccurrences(start, end, entry.Interval!.Value, new CalendarWindow(start, end))
            : new[] { start };
    }

    /// <summary>
    /// Lists the occurrences of an entry that fall inside <paramref name="window"/>. Occurrences
    /// are never prorated or scaled: each one either falls inside the window or it does not.
    /// </summary>
    /// <param name="entry">The entry to expand.</param>
    /// <param name="window">The inclusive window.</param>
    /// <returns>The occurrences inside the window, in ascending date order.</returns>
    public static IReadOnlyList<DateOnly> OccurrencesInWindow(MoneyEntry entry, CalendarWindow window)
    {
        DateOnly start = ToDate(entry.StartDate);
        DateOnly end = ToDate(entry.EndDate);

        if (end < start)
        {
            return Array.Empty<DateOnly>();
        }

        if (!IsRecurring(entry))
        {
            return window.Contains(start) ? new[] { start } : Array.Empty<DateOnly>();
        }

        return RecurringOccurrences(start, end, entry.Interval!.Value, window);
    }

    /// <summary>
    /// Totals the occurrences of every entry that fall inside <paramref name="window"/>, split
    /// into recurring and one-off amounts.
    /// </summary>
    /// <param name="entries">The entries to total.</param>
    /// <param name="window">The inclusive window.</param>
    /// <returns>The unrounded recurring and one-off sums for the window.</returns>
    public static OccurrenceTotals Totals(IEnumerable<MoneyEntry> entries, CalendarWindow window)
    {
        ArgumentNullException.ThrowIfNull(entries);

        double recurring = 0;
        double oneOff = 0;

        foreach (MoneyEntry entry in entries)
        {
            int count = OccurrencesInWindow(entry, window).Count;
            if (count == 0)
            {
                continue;
            }

            if (IsRecurring(entry))
            {
                recurring += entry.Value * count;
            }
            else
            {
                // A one-off is counted once and is never multiplied by the window length.
                oneOff += entry.Value;
            }
        }

        return new OccurrenceTotals(recurring, oneOff);
    }

    // The week starts on Monday: DayOfWeek runs Sunday (0) through Saturday (6), so this maps
    // Monday to 0 and Sunday to 6.
    private static int DaysSinceMonday(DateOnly date) => ((int)date.DayOfWeek + 6) % 7;

    private static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-DaysSinceMonday(date));

    private static CalendarWindow WeekFrom(DateOnly monday, int weeks) =>
        new CalendarWindow(monday, monday.AddDays((weeks * 7) - 1));

    private static CalendarWindow MonthWindow(int year, int month) =>
        new CalendarWindow(
            new DateOnly(year, month, 1),
            new DateOnly(year, month, DateTime.DaysInMonth(year, month)));

    private static CalendarWindow QuarterWindow(DateOnly today)
    {
        int firstMonth = (((today.Month - 1) / 3) * 3) + 1;
        DateOnly start = new DateOnly(today.Year, firstMonth, 1);

        // Three months from the first day, less one day, is the quarter's last day.
        return new CalendarWindow(start, start.AddMonths(3).AddDays(-1));
    }

    private static DateOnly ToDate(DateTime value) => DateOnly.FromDateTime(value);

    // Lists occurrences inside a window without walking the whole series from the original start
    // date, so a long-running daily entry does not cost one step per day since it began.
    private static IReadOnlyList<DateOnly> RecurringOccurrences(
        DateOnly start,
        DateOnly end,
        Interval interval,
        CalendarWindow window)
    {
        DateOnly limit = end < window.End ? end : window.End;
        if (limit < start || limit < window.Start)
        {
            return Array.Empty<DateOnly>();
        }

        List<DateOnly> occurrences = new List<DateOnly>();
        int step = FirstStepAtOrAfter(start, interval, window.Start);
        DateOnly occurrence = Step(start, interval, step);

        // The arithmetic estimate may land just before the window; walk forward until it is inside.
        while (occurrence < window.Start)
        {
            step++;
            occurrence = Step(start, interval, step);
        }

        // Every calendar step moves forward, so this terminates at the entry's own end date.
        while (occurrence <= limit)
        {
            occurrences.Add(occurrence);
            step++;
            occurrence = Step(start, interval, step);
        }

        return occurrences;
    }

    // An estimate that is never past the first in-window step. The caller advances from it, so an
    // underestimate is safe and an overestimate would skip occurrences; the arithmetic below is a
    // floor and therefore never skips.
    private static int FirstStepAtOrAfter(DateOnly start, Interval interval, DateOnly target)
    {
        if (target <= start)
        {
            return 0;
        }

        if (TryDaysPerStep(interval, out int days))
        {
            return (target.DayNumber - start.DayNumber) / days;
        }

        int stepMonths = MonthsPerStep(interval);
        int elapsedMonths = ((target.Year - start.Year) * 12) + target.Month - start.Month;

        return Math.Max(0, elapsedMonths / stepMonths);
    }

    // Calendar steps are always computed from the original start date, so the day-of-month stays
    // anchored: a series starting on 31 January steps to 28/29 February and then 31 March.
    private static DateOnly Step(DateOnly start, Interval interval, int step) => interval switch
    {
        Interval.Daily => start.AddDays(step),
        Interval.Weekly => start.AddDays(7 * step),
        Interval.BiWeekly => start.AddDays(14 * step),
        Interval.Monthly => start.AddMonths(step),
        Interval.Quarterly => start.AddMonths(3 * step),
        Interval.Yearly => start.AddMonths(12 * step),
        _ => throw new ArgumentOutOfRangeException(
            nameof(interval),
            interval,
            "Unsupported interval.")
    };

    private static bool TryDaysPerStep(Interval interval, out int days)
    {
        days = interval switch
        {
            Interval.Daily => 1,
            Interval.Weekly => 7,
            Interval.BiWeekly => 14,
            _ => 0
        };

        return days > 0;
    }

    private static int MonthsPerStep(Interval interval) => interval switch
    {
        Interval.Monthly => 1,
        Interval.Quarterly => 3,
        Interval.Yearly => 12,
        _ => throw new ArgumentOutOfRangeException(
            nameof(interval),
            interval,
            "Unsupported interval.")
    };
}

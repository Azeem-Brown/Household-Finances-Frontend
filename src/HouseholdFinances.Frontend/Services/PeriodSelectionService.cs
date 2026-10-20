using HouseholdFinances.Frontend.Models;

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// Frontend-only client service that holds the period selected for the expense figures. It is
/// scoped, following the <see cref="ThemeService"/> pattern, so Income and Bills share one
/// selection for the current Blazor Server circuit. Nothing is persisted, so a full reload starts
/// again from <see cref="Interval.Monthly"/>.
/// </summary>
public class PeriodSelectionService
{
    /// <summary>
    /// The selected period, defaulting to <see cref="Interval.Monthly"/> because that is the most
    /// common budgeting view and matches the Dashboard's companion work.
    /// </summary>
    public Interval Period { get; set; } = Interval.Monthly;
}

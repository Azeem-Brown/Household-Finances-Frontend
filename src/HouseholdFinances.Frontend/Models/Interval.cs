namespace HouseholdFinances.Frontend.Models;

/// <summary>
/// Cadence used by recurring Income, Bills, and Goals entries. Mirrors the backend
/// <c>HouseholdFinances.Domain.Entities.Interval</c> enum. The members are fixed by the recorded
/// Interval decision (#4): Daily, Weekly, BiWeekly, Monthly, Quarterly, Yearly.
/// </summary>
public enum Interval
{
    Daily = 0,
    Weekly = 1,
    BiWeekly = 2,
    Monthly = 3,
    Quarterly = 4,
    Yearly = 5
}

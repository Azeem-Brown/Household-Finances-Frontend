namespace HouseholdFinances.Frontend.Models;

/// <summary>
/// A labeled income entry owned by a user. Mirrors the specification "Income" schema and the
/// backend <c>HouseholdFinances.Domain.Entities.Income</c> entity. Income is linked to a household
/// through the owning user's membership, so it has no household key of its own.
/// </summary>
public class Income
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public double Value { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool Recurring { get; set; }

    /// <summary>
    /// The cadence used when <see cref="Recurring"/> is true; <see langword="null"/> for a one-off
    /// entry. Mirrors the backend <c>IncomeDetail.Interval</c> read model, which is nullable so a
    /// non-recurring entry is distinguishable from a daily one.
    /// </summary>
    public Interval? Interval { get; set; }

    public Guid UserId { get; set; }
}

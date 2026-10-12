namespace HouseholdFinances.Frontend.Models;

/// <summary>
/// A recurring or one-off bill for a household. Mirrors the specification "Bills" schema and the
/// backend <c>HouseholdFinances.Domain.Entities.Bill</c> entity.
/// </summary>
public class Bill
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public double Value { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool Recurring { get; set; }

    /// <summary>
    /// The cadence used when <see cref="Recurring"/> is true; <see langword="null"/> for a one-off
    /// bill. Mirrors the backend <c>BillDetail.Interval</c> read model, which is nullable so a
    /// non-recurring bill is distinguishable from a daily one.
    /// </summary>
    public Interval? Interval { get; set; }

    public Guid HouseholdId { get; set; }

    public Guid UserId { get; set; }
}

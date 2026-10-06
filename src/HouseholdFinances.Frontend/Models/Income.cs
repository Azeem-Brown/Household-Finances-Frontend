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

    public Interval Interval { get; set; }

    public Guid UserId { get; set; }
}

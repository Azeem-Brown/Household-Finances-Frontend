namespace HouseholdFinances.Frontend.Models;

/// <summary>
/// A savings goal for a household. Mirrors the specification "Goals" schema, including the Total
/// field carried over from the bill implementation, and the backend
/// <c>HouseholdFinances.Domain.Entities.Goal</c> entity.
/// </summary>
public class Goal
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public double Value { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool Recurring { get; set; }

    public Interval Interval { get; set; }

    public Guid HouseholdId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Amount contributed toward the goal so far.</summary>
    public double Total { get; set; }
}

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

    /// <summary>
    /// The cadence used when <see cref="Recurring"/> is true; <see langword="null"/> for a one-off
    /// goal that has no cadence. Mirrors the backend <c>GoalDetail.Interval</c> read model, which
    /// is nullable so a non-recurring goal is distinguishable from a daily one.
    /// </summary>
    public Interval? Interval { get; set; }

    public Guid HouseholdId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Amount contributed toward the goal so far.</summary>
    public double Total { get; set; }
}

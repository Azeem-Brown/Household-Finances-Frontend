namespace HouseholdFinances.Frontend.Models;

/// <summary>
/// A shared household. Mirrors the specification "Household" schema and the backend
/// <c>HouseholdFinances.Domain.Entities.Household</c> entity.
/// </summary>
public class Household
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Non-authoritative total, derived from the household members' income entries.
    /// </summary>
    public double Incomes { get; set; }

    /// <summary>
    /// Non-authoritative total, derived from the household members' bill entries.
    /// </summary>
    public double Payments { get; set; }
}

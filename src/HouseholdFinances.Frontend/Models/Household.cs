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

    /// <summary>
    /// The number of stored income entries for the household, independent of the requested period.
    /// The Dashboard uses it to tell "no entries at all" apart from "no occurrences in this window".
    /// </summary>
    public int IncomeEntryCount { get; set; }

    /// <summary>
    /// The number of stored bill entries for the household, independent of the requested period.
    /// The Dashboard uses it to tell "no entries at all" apart from "no occurrences in this window".
    /// </summary>
    public int BillEntryCount { get; set; }
}

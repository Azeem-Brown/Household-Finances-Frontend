namespace HouseholdFinances.Frontend.Models;

/// <summary>
/// A priced item belonging to a household. Mirrors the specification "Items" schema and the
/// backend <c>HouseholdFinances.Domain.Entities.Item</c> entity. Items are modeled for
/// completeness; no Items page is described in the specification.
/// </summary>
public class Item
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional free-text description; null when the specification allows no description.
    /// </summary>
    public string? Description { get; set; }

    public double Price { get; set; }

    public Guid HouseholdId { get; set; }
}

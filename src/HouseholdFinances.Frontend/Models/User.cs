namespace HouseholdFinances.Frontend.Models;

/// <summary>
/// A person who owns income, bills, and goals. Mirrors the specification "Users" schema and the
/// backend <c>HouseholdFinances.Domain.Entities.User</c> entity so both repositories agree on the
/// field names and types.
/// </summary>
public class User
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Retained to match the specification schema (User.Password is nullable). Authentication is
    /// third party (Google Identity), so this is unused and password hashes are never returned by
    /// the API.
    /// </summary>
    public string? Password { get; set; }
}

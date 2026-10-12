namespace HouseholdFinances.Frontend.Models;

/// <summary>
/// Read model returned by <c>GET /api/v1/users/me</c> for the signed-in user. Mirrors the backend
/// <c>HouseholdFinances.Domain.Models.UserProfile</c> contract so both repositories agree on the
/// field names and types. It deliberately carries no password member: the third-party identity
/// provider owns credentials, so the API never returns one.
/// </summary>
public class UserProfile
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The households the user belongs to. Empty when the user has no household membership, which
    /// is what routes a newly signed-in user to <c>/setup</c> rather than <c>/dashboard</c>.
    /// </summary>
    public List<Household> Households { get; set; } = new();
}

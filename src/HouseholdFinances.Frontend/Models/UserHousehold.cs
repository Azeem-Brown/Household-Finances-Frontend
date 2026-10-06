namespace HouseholdFinances.Frontend.Models;

/// <summary>
/// Join row linking a user to a household. Mirrors the specification "User Household" schema and
/// the backend <c>HouseholdFinances.Domain.Entities.UserHousehold</c> entity. Household membership
/// is many-to-many through this type.
/// </summary>
public class UserHousehold
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid HouseholdId { get; set; }
}

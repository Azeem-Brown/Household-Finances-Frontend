namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// Where an authenticated user should be routed after a successful sign-in, decided from the
/// account's household state.
/// </summary>
public enum AuthenticationDestination
{
    /// <summary>The user has no household yet and must complete household setup.</summary>
    Setup = 0,

    /// <summary>The user already belongs to a household and can open the dashboard.</summary>
    Dashboard = 1
}

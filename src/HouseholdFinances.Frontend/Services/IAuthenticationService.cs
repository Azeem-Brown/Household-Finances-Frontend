namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// The frontend entry point for authenticating a user with the third-party (Google) provider.
/// </summary>
/// <remarks>
/// This is the client-side service surface for the Login page (issue #9). The concrete
/// implementation that runs the Google Identity flow and exchanges the Google token for the
/// API-issued JWT belongs to backend issue #11 ("Implement Google Identity authentication with
/// API-issued JWT") and is intentionally not implemented here. Until then the app registers
/// <see cref="UnavailableAuthenticationService"/>, which reports the sign-in as unavailable using
/// the shared client error convention rather than pretending to authenticate.
/// </remarks>
public interface IAuthenticationService
{
    /// <summary>
    /// Starts the third-party sign-in flow and returns its outcome. The implementation is
    /// responsible for obtaining the provider token and an authenticated session, and for
    /// reporting which destination the authenticated user should be routed to.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the sign-in attempt.</param>
    /// <returns>The sign-in outcome.</returns>
    Task<AuthenticationResult> SignInAsync(CancellationToken cancellationToken = default);
}

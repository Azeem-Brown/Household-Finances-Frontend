namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// The default <see cref="IAuthenticationService"/> registration used until the real Google
/// Identity sign-in is delivered by backend issue #11.
/// </summary>
/// <remarks>
/// This is an explicit "not available yet" boundary, not a working authentication stub: it never
/// succeeds and never fabricates a token or a session. Failing through
/// <see cref="AuthenticationResult.Failure"/> keeps the app runnable and lets the Login page
/// demonstrate the shared client error convention instead of throwing an unauthenticated
/// dependency-injection failure. Replace this registration with the backend-backed implementation
/// once the API contract is documented and implemented.
/// </remarks>
public sealed class UnavailableAuthenticationService : IAuthenticationService
{
    /// <summary>
    /// The identifier of significance shown with the failure. It names the missing piece - the
    /// authentication service itself - rather than a user or credential, so the display reads
    /// <c>Unknown AuthenticationUnavailable</c>.
    /// </summary>
    public const string UnavailableIdentifier = "AuthenticationUnavailable";

    /// <inheritdoc />
    public Task<AuthenticationResult> SignInAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(AuthenticationResult.Failure(ClientErrorCode.Unknown, UnavailableIdentifier));
}

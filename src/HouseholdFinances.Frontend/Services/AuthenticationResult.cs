namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// The outcome of a sign-in attempt. A successful result carries the
/// <see cref="AuthenticationDestination"/> the user should be routed to; a failed result carries a
/// <see cref="ClientErrorCode"/> and the identifier or number of significance that was being
/// reached, matching the shared client error convention from issue #7.
/// </summary>
/// <remarks>
/// The type is deliberately free of any wire contract: how the third-party token is exchanged for
/// an authenticated session, and how household state is determined, belong to the authentication
/// service owned by backend issue #11.
/// </remarks>
public sealed class AuthenticationResult
{
    private AuthenticationResult(
        bool succeeded,
        AuthenticationDestination? destination,
        ClientErrorCode? errorCode,
        object? identifier)
    {
        Succeeded = succeeded;
        Destination = destination;
        ErrorCode = errorCode;
        Identifier = identifier;
    }

    /// <summary>Gets whether the sign-in attempt succeeded.</summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets the destination to route to when <see cref="Succeeded"/> is <see langword="true"/>;
    /// otherwise <see langword="null"/>.
    /// </summary>
    public AuthenticationDestination? Destination { get; }

    /// <summary>
    /// Gets the failure category when <see cref="Succeeded"/> is <see langword="false"/>; otherwise
    /// <see langword="null"/>.
    /// </summary>
    public ClientErrorCode? ErrorCode { get; }

    /// <summary>
    /// Gets the identifier or number of significance that was being reached when the failure
    /// occurred; otherwise <see langword="null"/>.
    /// </summary>
    public object? Identifier { get; }

    /// <summary>
    /// Gets the failure message formatted with the shared client error convention, or
    /// <see langword="null"/> when the attempt succeeded.
    /// </summary>
    public string? ErrorMessage =>
        Succeeded ? null : ClientErrorFormatter.Format(ErrorCode!.Value, Identifier!);

    /// <summary>Creates a successful result that routes to the supplied destination.</summary>
    /// <param name="destination">Where the authenticated user should be routed.</param>
    public static AuthenticationResult Success(AuthenticationDestination destination) =>
        new(succeeded: true, destination, errorCode: null, identifier: null);

    /// <summary>Creates a failed result using the shared client error convention.</summary>
    /// <param name="errorCode">The client-side error category.</param>
    /// <param name="identifier">The identifier or number of significance that was being reached.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="identifier"/> is <see langword="null"/>.
    /// </exception>
    public static AuthenticationResult Failure(ClientErrorCode errorCode, object identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        return new(succeeded: false, destination: null, errorCode, identifier);
    }
}

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// The session identifier of the current circuit, learned from the HTTP request that prerendered
/// it and carried across the prerender boundary by <c>SessionStateBridge</c>.
/// </summary>
/// <remarks>
/// The identifier is not a secret on its own, but it is the only thing that locates the stored
/// token pair, so it is never rendered, logged, or sent to the browser in a readable form: the
/// browser only carries it in an HTTP-only cookie.
/// </remarks>
public interface ISessionIdAccessor
{
    /// <summary>Gets the session identifier, or <see langword="null"/> before one has been resolved.</summary>
    string? SessionId { get; }

    /// <summary>Records the session identifier for the current circuit.</summary>
    /// <param name="sessionId">The opaque identifier from the session cookie.</param>
    void SetSessionId(string sessionId);
}

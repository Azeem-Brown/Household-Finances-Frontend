namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// Durable, encrypted storage for the token pair of one browser session (issue #28).
/// </summary>
/// <remarks>
/// Records are keyed by the opaque session identifier held in the browser's HTTP-only cookie, so
/// the browser never holds a token. Every read that finds a missing, unreadable, or expired record
/// removes it and reports no pair, which is what makes a stale persisted value fail closed.
/// </remarks>
public interface ISessionTokenRepository
{
    /// <summary>
    /// Reads the token pair stored for a session.
    /// </summary>
    /// <param name="sessionId">The opaque session identifier from the session cookie.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The stored pair, or <see langword="null"/> when no usable pair is stored.</returns>
    Task<TokenPair?> GetAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the token pair stored for a session. Called on sign-in and on every refresh
    /// rotation, so the record always holds the newest refresh token.
    /// </summary>
    /// <param name="sessionId">The opaque session identifier from the session cookie.</param>
    /// <param name="tokens">The pair to store.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    Task SetAsync(string sessionId, TokenPair tokens, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the token pair stored for a session, if any.
    /// </summary>
    /// <param name="sessionId">The opaque session identifier from the session cookie.</param>
    /// <param name="cancellationToken">A token to cancel the clear.</param>
    Task ClearAsync(string sessionId, CancellationToken cancellationToken = default);
}

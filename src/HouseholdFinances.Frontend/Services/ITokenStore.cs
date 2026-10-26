namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// Holds the current API token pair for the signed-in user and lets the API client read, replace,
/// and clear it.
/// </summary>
/// <remarks>
/// The default implementation is <see cref="PersistentTokenStore"/> (issue #28): it keeps the
/// pair in encrypted server-side storage keyed by the browser's opaque session id, so a reload or
/// an app restart resumes the session while the refresh token is valid. <see cref="InMemoryTokenStore"/>
/// remains for tests and for hosts that deliberately keep the pair only for the life of the
/// circuit. The store is the only persistence seam, so the API client and its callers are
/// unaffected by which implementation is registered.
/// </remarks>
public interface ITokenStore
{
    /// <summary>
    /// Gets the current token pair.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The stored pair, or <see langword="null"/> when no pair is stored.</returns>
    Task<TokenPair?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the stored token pair with the supplied pair.
    /// </summary>
    /// <param name="tokens">The pair to store. Both tokens are replaced together.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    Task SetAsync(TokenPair tokens, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes any stored token pair.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the clear.</param>
    Task ClearAsync(CancellationToken cancellationToken = default);
}

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// Holds the current API token pair for the signed-in user and lets the API client read, replace,
/// and clear it.
/// </summary>
/// <remarks>
/// The default implementation keeps the pair in memory for the life of the Blazor Server circuit.
/// Persisting it across a browser reload or an app restart is issue #28 and is a matter of
/// registering a different implementation: the asynchronous surface exists so a store backed by
/// durable state (for example an encrypted cookie) can be substituted without changing the API
/// client or its callers.
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

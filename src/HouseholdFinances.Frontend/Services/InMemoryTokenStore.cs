namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// A circuit-scoped, in-memory <see cref="ITokenStore"/>.
/// </summary>
/// <remarks>
/// The pair lives only for the life of the Blazor Server circuit and is lost on a full browser
/// reload or an app restart. Persisting the session is issue #28, which swaps this implementation
/// for a durable one rather than changing the API client.
/// </remarks>
public sealed class InMemoryTokenStore : ITokenStore
{
    private TokenPair? _tokens;

    /// <inheritdoc />
    public Task<TokenPair?> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_tokens);

    /// <inheritdoc />
    public Task SetAsync(TokenPair tokens, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        _tokens = tokens;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _tokens = null;
        return Task.CompletedTask;
    }
}

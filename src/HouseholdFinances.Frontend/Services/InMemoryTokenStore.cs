namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// A circuit-scoped, in-memory <see cref="ITokenStore"/>.
/// </summary>
/// <remarks>
/// The pair lives only for the life of the Blazor Server circuit and is lost on a full browser
/// reload or an app restart. The application registers <see cref="PersistentTokenStore"/> instead
/// (issue #28); this implementation stays as a lightweight double for tests and as a reference for
/// the store contract.
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

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// The durable <see cref="ITokenStore"/>: it reads and writes the token pair of the current
/// circuit's session through <see cref="ISessionTokenRepository"/> (issue #28).
/// </summary>
/// <remarks>
/// Nothing is cached in memory beyond the life of a single call, so sign-out, a failed refresh, or
/// an expired record takes effect immediately for the next request. When the circuit has no
/// session identifier the store throws instead of quietly falling back to memory: a silent
/// in-memory fallback would look like a working session that disappears on reload.
/// </remarks>
public sealed class PersistentTokenStore : ITokenStore
{
    private readonly ISessionTokenRepository _repository;
    private readonly ISessionIdAccessor _sessionIdAccessor;

    /// <summary>Creates the store.</summary>
    /// <param name="repository">The durable, encrypted session record storage.</param>
    /// <param name="sessionIdAccessor">The accessor holding the current circuit's session id.</param>
    public PersistentTokenStore(ISessionTokenRepository repository, ISessionIdAccessor sessionIdAccessor)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _sessionIdAccessor = sessionIdAccessor ?? throw new ArgumentNullException(nameof(sessionIdAccessor));
    }

    /// <inheritdoc />
    public Task<TokenPair?> GetAsync(CancellationToken cancellationToken = default) =>
        _repository.GetAsync(RequireSessionId(), cancellationToken);

    /// <inheritdoc />
    public Task SetAsync(TokenPair tokens, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        return _repository.SetAsync(RequireSessionId(), tokens, cancellationToken);
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        _repository.ClearAsync(RequireSessionId(), cancellationToken);

    private string RequireSessionId() => _sessionIdAccessor.SessionId ?? throw new InvalidOperationException(
        "No session identifier is available for this circuit, so the token pair cannot be persisted. "
        + "The identifier is carried from the prerendered HTTP request into the circuit by SessionStateBridge; "
        + "check that interactive server components are prerendered and that SessionCookieMiddleware is registered.");
}

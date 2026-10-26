namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// The circuit-scoped <see cref="ISessionIdAccessor"/>. Setting an identifier marks it as held by
/// this circuit, and disposing the scope (the circuit ending) releases it, so the HTTP pipeline
/// knows which cookies a live circuit still depends on.
/// </summary>
public sealed class SessionIdAccessor : ISessionIdAccessor, IDisposable
{
    private readonly ActiveSessionRegistry _registry;
    private string? _sessionId;

    /// <summary>Creates the accessor.</summary>
    /// <param name="registry">The registry tracking identifiers held by live circuits.</param>
    public SessionIdAccessor(ActiveSessionRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <inheritdoc />
    public string? SessionId => Volatile.Read(ref _sessionId);

    /// <inheritdoc />
    public void SetSessionId(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        Volatile.Write(ref _sessionId, sessionId);
        _registry.Add(sessionId);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        var sessionId = Interlocked.Exchange(ref _sessionId, null);
        if (sessionId is not null)
        {
            _registry.Remove(sessionId);
        }
    }
}

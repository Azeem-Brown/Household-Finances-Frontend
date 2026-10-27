namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// Tracks the session identifiers held by live Blazor Server circuits in this process.
/// </summary>
/// <remarks>
/// A circuit cannot delete its own cookie, so the cookie is cleaned up by a later HTTP request.
/// That request must not replace an identifier a live circuit is still using - the circuit may be
/// part-way through signing in - so the middleware only replaces a cookie whose identifier is
/// absent from this registry and has no server-side record. Entries are released when the circuit
/// scope is disposed, and the registry is process-local by design (it only guards one process).
/// </remarks>
public sealed class ActiveSessionRegistry
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _active = new(StringComparer.Ordinal);

    /// <summary>Marks a session identifier as held by a live circuit.</summary>
    /// <param name="sessionId">The session identifier.</param>
    public void Add(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        _active.AddOrUpdate(sessionId, 1, static (_, count) => count + 1);
    }

    /// <summary>Releases one reference to a session identifier.</summary>
    /// <param name="sessionId">The session identifier.</param>
    public void Remove(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        while (_active.TryGetValue(sessionId, out int count))
        {
            bool released = count <= 1
                ? _active.TryRemove(new KeyValuePair<string, int>(sessionId, count))
                : _active.TryUpdate(sessionId, count - 1, count);

            if (released)
            {
                return;
            }
        }
    }

    /// <summary>Gets whether any live circuit holds the session identifier.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns><see langword="true"/> when a circuit holds the identifier.</returns>
    public bool IsActive(string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        return _active.ContainsKey(sessionId);
    }
}

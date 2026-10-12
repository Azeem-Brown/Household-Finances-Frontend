using HouseholdFinances.Frontend.Services;

namespace HouseholdFinances.Frontend.Tests.Services;

/// <summary>
/// A controllable <see cref="IGoogleIdentityProvider"/> test double standing in for the browser
/// Google Identity Services interop, so the sign-in flow can be tested without a browser or any
/// real Google credentials.
/// </summary>
internal sealed class StubGoogleIdentityProvider : IGoogleIdentityProvider
{
    /// <summary>Gets or sets the ID token to return. <see langword="null"/> simulates a cancel.</summary>
    public string? IdToken { get; set; }

    /// <summary>Gets or sets an exception to throw instead of returning, simulating interop failure.</summary>
    public Exception? Throw { get; set; }

    /// <summary>Gets the client id the provider was called with.</summary>
    public string? ClientId { get; private set; }

    /// <summary>Gets the number of times <see cref="GetIdTokenAsync"/> was called.</summary>
    public int CallCount { get; private set; }

    /// <inheritdoc />
    public Task<string?> GetIdTokenAsync(string clientId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        CallCount++;
        ClientId = clientId;

        if (Throw is not null)
        {
            throw Throw;
        }

        return Task.FromResult(IdToken);
    }
}

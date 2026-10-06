using HouseholdFinances.Frontend.Services;

namespace HouseholdFinances.Frontend.Tests.Components;

/// <summary>
/// A controllable <see cref="IAuthenticationService"/> test double. It lets the Login page tests
/// drive success, failure, and post-login routing without any real provider or backend.
/// </summary>
internal sealed class StubAuthenticationService : IAuthenticationService
{
    /// <summary>Gets or sets the result the next sign-in attempt returns.</summary>
    public AuthenticationResult Result { get; set; } =
        AuthenticationResult.Failure(ClientErrorCode.Unknown, "NotConfigured");

    /// <summary>Gets the number of times <see cref="SignInAsync"/> was called.</summary>
    public int CallCount { get; private set; }

    /// <inheritdoc />
    public Task<AuthenticationResult> SignInAsync(CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(Result);
    }
}

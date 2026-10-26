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

    /// <summary>Gets or sets an exception to throw instead of returning a result.</summary>
    public Exception? Throw { get; set; }

    /// <summary>Gets the number of times <see cref="SignInAsync"/> was called.</summary>
    public int CallCount { get; private set; }

    /// <summary>Gets or sets whether a session is reported as stored.</summary>
    public bool HasSession { get; set; }

    /// <summary>Gets the number of times <see cref="SignOutAsync"/> was called.</summary>
    public int SignOutCallCount { get; private set; }

    /// <inheritdoc />
    public Task<bool> HasSessionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(HasSession);

    /// <inheritdoc />
    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        SignOutCallCount++;
        HasSession = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<AuthenticationResult> SignInAsync(CancellationToken cancellationToken = default)
    {
        CallCount++;

        return Throw is null
            ? Task.FromResult(Result)
            : Task.FromException<AuthenticationResult>(Throw);
    }
}

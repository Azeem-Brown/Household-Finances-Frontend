using HouseholdFinances.Frontend.Services;

namespace HouseholdFinances.Frontend.Tests.Components;

/// <summary>
/// A controllable <see cref="IApiClient"/> test double for component tests. It records the
/// authenticated POST calls a component makes and lets a test return a response or throw, so the
/// Setup page can be exercised without a live backend.
/// </summary>
internal sealed class StubApiClient : IApiClient
{
    /// <summary>Gets the authenticated POST calls that were made, in order.</summary>
    public List<RecordedPost> Posts { get; } = new();

    /// <summary>Gets or sets an exception to throw instead of returning a response.</summary>
    public Exception? Throw { get; set; }

    /// <inheritdoc />
    public Task<TResponse> PostAsync<TRequest, TResponse>(
        string path,
        TRequest body,
        CancellationToken cancellationToken = default)
    {
        Posts.Add(new RecordedPost(path, body));

        return Throw is null
            ? Task.FromResult(default(TResponse)!)
            : Task.FromException<TResponse>(Throw);
    }

    /// <inheritdoc />
    public Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The Setup page never sends an authenticated GET.");

    /// <inheritdoc />
    public Task PostAsync<TRequest>(string path, TRequest body, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The Setup page never sends a bodyless POST.");

    /// <inheritdoc />
    public Task<TResponse> PatchAsync<TRequest, TResponse>(
        string path,
        TRequest body,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The Setup page never sends a PATCH.");

    /// <inheritdoc />
    public Task DeleteAsync(string path, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The Setup page never sends a DELETE.");

    /// <inheritdoc />
    public Task<TResponse> PostAnonymousAsync<TRequest, TResponse>(
        string path,
        TRequest body,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The Setup page never sends an anonymous POST.");

    /// <summary>A recorded authenticated POST call.</summary>
    /// <param name="Path">The API path the call targeted.</param>
    /// <param name="Body">The request body, kept as its declared type for inspection.</param>
    internal sealed record RecordedPost(string Path, object? Body);
}

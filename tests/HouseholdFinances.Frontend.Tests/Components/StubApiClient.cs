using HouseholdFinances.Frontend.Services;

namespace HouseholdFinances.Frontend.Tests.Components;

/// <summary>
/// A controllable <see cref="IApiClient"/> test double for component tests. It records the
/// authenticated GET and POST calls a component makes and lets a test return a response or throw,
/// so pages such as Setup and Dashboard can be exercised without a live backend.
/// </summary>
internal sealed class StubApiClient : IApiClient
{
    /// <summary>Gets the authenticated POST calls that were made, in order.</summary>
    public List<RecordedPost> Posts { get; } = new();

    /// <summary>Gets the authenticated GET paths that were requested, in order.</summary>
    public List<string> Gets { get; } = new();

    /// <summary>Gets or sets the response returned by <see cref="GetAsync{TResponse}"/>.</summary>
    public object? GetResponse { get; set; }

    /// <summary>
    /// Gets or sets an exception to throw instead of returning a response from
    /// <see cref="GetAsync{TResponse}"/> or <see cref="PostAsync{TRequest, TResponse}"/>.
    /// </summary>
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
    public Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancellationToken = default)
    {
        Gets.Add(path);

        return Throw is null
            ? Task.FromResult((TResponse)GetResponse!)
            : Task.FromException<TResponse>(Throw);
    }

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

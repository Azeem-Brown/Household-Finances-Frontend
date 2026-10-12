using HouseholdFinances.Frontend.Services;

namespace HouseholdFinances.Frontend.Tests.Components;

/// <summary>
/// A controllable <see cref="IApiClient"/> test double for component tests. It records the
/// authenticated GET, POST, PUT, and DELETE calls a component makes and lets a test return a
/// response or throw, so pages such as Setup, Dashboard, and Income can be exercised without a
/// live backend.
/// </summary>
internal sealed class StubApiClient : IApiClient
{
    /// <summary>Gets the authenticated POST calls that were made, in order.</summary>
    public List<RecordedPost> Posts { get; } = new();

    /// <summary>Gets the authenticated PUT calls that were made, in order.</summary>
    public List<RecordedPost> Puts { get; } = new();

    /// <summary>Gets the authenticated DELETE paths that were requested, in order.</summary>
    public List<string> Deletes { get; } = new();

    /// <summary>Gets the authenticated GET paths that were requested, in order.</summary>
    public List<string> Gets { get; } = new();

    /// <summary>
    /// Gets or sets the response returned by <see cref="GetAsync{TResponse}"/> when no path-specific
    /// response is registered in <see cref="GetResponsesByPath"/>.
    /// </summary>
    public object? GetResponse { get; set; }

    /// <summary>
    /// Gets per-path responses for <see cref="GetAsync{TResponse}"/>, used when a component makes
    /// several GETs that return different shapes (for example the household list and then that
    /// household's income). A path registered here takes precedence over <see cref="GetResponse"/>.
    /// </summary>
    public Dictionary<string, object?> GetResponsesByPath { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets an exception to throw instead of returning a response from
    /// <see cref="GetAsync{TResponse}"/> or <see cref="PostAsync{TRequest, TResponse}"/>.
    /// </summary>
    public Exception? Throw { get; set; }

    /// <summary>
    /// Gets per-path exceptions for <see cref="GetAsync{TResponse}"/>, so a test can fail one call
    /// (for example a nested income request returning NotFound) while earlier calls succeed. A path
    /// registered here takes precedence over <see cref="Throw"/>.
    /// </summary>
    public Dictionary<string, Exception> ThrowByPath { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets per-path exceptions for <see cref="PutAsync{TRequest, TResponse}"/>, so a test can fail
    /// a single update while earlier calls succeed. A path registered here takes precedence over
    /// <see cref="Throw"/>.
    /// </summary>
    public Dictionary<string, Exception> ThrowByPathForPut { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets per-path exceptions for <see cref="DeleteAsync}"/>. A path registered here takes
    /// precedence over <see cref="Throw"/>.
    /// </summary>
    public Dictionary<string, Exception> ThrowByPathForDelete { get; } = new(StringComparer.Ordinal);

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

        if (ThrowByPath.TryGetValue(path, out var pathThrow))
        {
            return Task.FromException<TResponse>(pathThrow);
        }

        if (Throw is not null)
        {
            return Task.FromException<TResponse>(Throw);
        }

        var response = GetResponsesByPath.TryGetValue(path, out var mapped) ? mapped : GetResponse;

        return Task.FromResult((TResponse)response!);
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
    public Task<TResponse> PutAsync<TRequest, TResponse>(
        string path,
        TRequest body,
        CancellationToken cancellationToken = default)
    {
        Puts.Add(new RecordedPost(path, body));

        if (ThrowByPathForPut.TryGetValue(path, out var pathThrow))
        {
            return Task.FromException<TResponse>(pathThrow);
        }

        return Throw is null
            ? Task.FromResult(default(TResponse)!)
            : Task.FromException<TResponse>(Throw);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        Deletes.Add(path);

        if (ThrowByPathForDelete.TryGetValue(path, out var pathThrow))
        {
            return Task.FromException(pathThrow);
        }

        return Throw is null ? Task.CompletedTask : Task.FromException(Throw);
    }

    /// <inheritdoc />
    public Task<TResponse> PostAnonymousAsync<TRequest, TResponse>(
        string path,
        TRequest body,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The Setup page never sends an anonymous POST.");

    /// <summary>A recorded authenticated POST or PUT call.</summary>
    /// <param name="Path">The API path the call targeted.</param>
    /// <param name="Body">The request body, kept as its declared type for inspection.</param>
    internal sealed record RecordedPost(string Path, object? Body);
}

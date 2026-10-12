namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// The shared transport layer for calling the backend API.
/// </summary>
/// <remarks>
/// Requests are sent as camelCase JSON beneath the configured base URL. Authenticated calls attach
/// <c>Authorization: Bearer &lt;accessToken&gt;</c> while a token is stored, and recover once from
/// an expired access token by refreshing the stored pair and retrying the original request once.
/// Every non-success response and every transport failure surfaces as a
/// <see cref="ClientErrorException"/> so callers never handle raw HTTP or JSON failures.
/// </remarks>
public interface IApiClient
{
    /// <summary>
    /// Sends an authenticated GET and deserializes the JSON response body.
    /// </summary>
    /// <typeparam name="TResponse">The response body type.</typeparam>
    /// <param name="path">The API path, for example <c>/api/v1/users/me</c>.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The deserialized response body.</returns>
    Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an authenticated POST with a JSON body and deserializes the JSON response body.
    /// </summary>
    /// <typeparam name="TRequest">The request body type.</typeparam>
    /// <typeparam name="TResponse">The response body type.</typeparam>
    /// <param name="path">The API path, for example <c>/api/v1/households</c>.</param>
    /// <param name="body">The request body, serialized as camelCase JSON.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The deserialized response body.</returns>
    Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an authenticated POST with a JSON body that returns no content.
    /// </summary>
    /// <typeparam name="TRequest">The request body type.</typeparam>
    /// <param name="path">The API path.</param>
    /// <param name="body">The request body, serialized as camelCase JSON.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    Task PostAsync<TRequest>(string path, TRequest body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an authenticated PATCH with a JSON body and deserializes the JSON response body.
    /// </summary>
    /// <typeparam name="TRequest">The request body type.</typeparam>
    /// <typeparam name="TResponse">The response body type.</typeparam>
    /// <param name="path">The API path.</param>
    /// <param name="body">The request body, serialized as camelCase JSON.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The deserialized response body.</returns>
    Task<TResponse> PatchAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an authenticated DELETE that returns no content.
    /// </summary>
    /// <param name="path">The API path.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a POST without a bearer token and without refresh handling, for the anonymous
    /// authentication endpoints such as <c>POST /api/v1/auth/google</c>.
    /// </summary>
    /// <typeparam name="TRequest">The request body type.</typeparam>
    /// <typeparam name="TResponse">The response body type.</typeparam>
    /// <param name="path">The API path, for example <c>/api/v1/auth/google</c>.</param>
    /// <param name="body">The request body, serialized as camelCase JSON.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The deserialized response body.</returns>
    Task<TResponse> PostAnonymousAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default);
}

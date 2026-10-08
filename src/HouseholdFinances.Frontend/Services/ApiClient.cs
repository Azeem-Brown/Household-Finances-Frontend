using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// The default <see cref="IApiClient"/>. It sends camelCase JSON matching the backend conventions,
/// attaches the stored bearer token, refreshes the token pair once on a 401 and retries the
/// original request once, and maps <c>application/problem+json</c> failures onto the shared
/// <see cref="ClientErrorException"/> convention.
/// </summary>
public sealed class ApiClient : IApiClient
{
    /// <summary>The path used to exchange a refresh token for a new pair.</summary>
    public const string RefreshPath = "/api/v1/auth/refresh";

    // The same wire conventions the backend applies (camelCase, case-insensitive reads, ISO 8601
    // date-times). Enums stay numeric, matching the backend's documented convention.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ITokenStore _tokenStore;
    private readonly ILogger<ApiClient> _logger;

    /// <summary>Creates the client.</summary>
    /// <param name="httpClient">
    /// The HTTP client carrying the configured API base address. Requests are always relative to it.
    /// </param>
    /// <param name="tokenStore">The store holding the current token pair.</param>
    /// <param name="logger">The logger used to record refresh and transport failures.</param>
    public ApiClient(HttpClient httpClient, ITokenStore tokenStore, ILogger<ApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancellationToken = default) =>
        SendAuthenticatedAsync<TResponse>(HttpMethod.Get, path, body: null, cancellationToken);

    /// <inheritdoc />
    public Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default) =>
        SendAuthenticatedAsync<TResponse>(HttpMethod.Post, path, body, cancellationToken);

    /// <inheritdoc />
    public async Task PostAsync<TRequest>(string path, TRequest body, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedRawAsync(HttpMethod.Post, path, body, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResponse> PatchAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default) =>
        SendAuthenticatedAsync<TResponse>(HttpMethod.Patch, path, body, cancellationToken);

    /// <inheritdoc />
    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthenticatedRawAsync(HttpMethod.Delete, path, body: null, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TResponse> PostAnonymousAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, path, body, accessToken: null, cancellationToken);
        return await ReadRequiredAsync<TResponse>(response, cancellationToken);
    }

    private async Task<TResponse> SendAuthenticatedAsync<TResponse>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        using var response = await SendAuthenticatedRawAsync(method, path, body, cancellationToken);
        return await ReadRequiredAsync<TResponse>(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAuthenticatedRawAsync(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        var tokens = await _tokenStore.GetAsync(cancellationToken);
        var response = await SendAsync(method, path, body, tokens?.AccessToken, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized ||
            string.IsNullOrEmpty(tokens?.RefreshToken))
        {
            return response;
        }

        // The access token was rejected: dispose the first attempt, refresh the pair once, and
        // retry the original request once with the replacement token.
        response.Dispose();

        var refreshed = await RefreshAsync(tokens!.RefreshToken!, path, cancellationToken);
        await _tokenStore.SetAsync(refreshed, cancellationToken);

        return await SendAsync(method, path, body, refreshed.AccessToken, cancellationToken);
    }

    private async Task<TokenPair> RefreshAsync(
        string refreshToken,
        string originalPath,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Refreshing the API token pair after an unauthorized response.");

        HttpResponseMessage response;
        try
        {
            response = await SendAsync(
                HttpMethod.Post,
                RefreshPath,
                new RefreshTokenRequest(refreshToken),
                accessToken: null,
                cancellationToken);
        }
        catch (ClientErrorException exception)
        {
            // The refresh endpoint could not be reached: the session cannot be recovered.
            _logger.LogWarning(exception, "The token refresh request could not be completed.");
            throw await FailRefreshAsync(originalPath, exception, cancellationToken);
        }

        using (response)
        {
            var refreshed = response.IsSuccessStatusCode
                ? await TryReadAsync<TokenPair>(response, cancellationToken)
                : null;

            if (refreshed is null)
            {
                object identifier = response.IsSuccessStatusCode ? originalPath : (int)response.StatusCode;
                _logger.LogWarning("The token refresh request failed with status {Status}.", (int)response.StatusCode);
                throw await FailRefreshAsync(identifier, innerException: null, cancellationToken: cancellationToken);
            }

            return refreshed;
        }
    }

    private async Task<ClientErrorException> FailRefreshAsync(
        object identifier,
        Exception? innerException,
        CancellationToken cancellationToken)
    {
        await _tokenStore.ClearAsync(cancellationToken);
        return new ClientErrorException(ClientErrorCode.Unauthorized, identifier, innerException);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        object? body,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
        }

        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        try
        {
            return await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "The API request to {Path} failed because the API was unreachable.", path);
            throw new ClientErrorException(ClientErrorCode.Unknown, path, exception);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("The API request to {Path} timed out.", path);
            throw new ClientErrorException(ClientErrorCode.Unknown, path);
        }
    }

    private async Task<TResponse> ReadRequiredAsync<TResponse>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);

        TResponse? value;
        try
        {
            value = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new ClientErrorException(ClientErrorCode.Unknown, (int)response.StatusCode, exception);
        }
        catch (NotSupportedException exception)
        {
            throw new ClientErrorException(ClientErrorCode.Unknown, (int)response.StatusCode, exception);
        }

        return value ?? throw new ClientErrorException(ClientErrorCode.Unknown, (int)response.StatusCode);
    }

    private static async Task<T?> TryReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return default;
        }
        catch (NotSupportedException)
        {
            return default;
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw await CreateErrorAsync(response, cancellationToken);
    }

    private static async Task<ClientErrorException> CreateErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var identifier = (object)(int)response.StatusCode;
        var errorCode = ClientErrorCode.Unknown;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;

                // Only a body carrying a numeric errorCode is treated as the problem+json
                // convention; anything else falls back to Unknown with the HTTP status.
                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("errorCode", out var errorCodeElement) &&
                    errorCodeElement.TryGetInt32(out var numericErrorCode))
                {
                    errorCode = MapErrorCode(numericErrorCode);

                    if (root.TryGetProperty("identifier", out var identifierElement))
                    {
                        var identifierText = identifierElement.ValueKind == JsonValueKind.String
                            ? identifierElement.GetString()
                            : identifierElement.ToString();

                        if (!string.IsNullOrEmpty(identifierText))
                        {
                            identifier = identifierText;
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Not JSON: keep the Unknown/status fallback.
            }
        }

        return new ClientErrorException(errorCode, identifier);
    }

    private static ClientErrorCode MapErrorCode(int errorCode) => errorCode switch
    {
        1 => ClientErrorCode.NotFound,
        2 => ClientErrorCode.InvalidInput,
        3 => ClientErrorCode.Unauthorized,
        4 => ClientErrorCode.Conflict,
        5 => ClientErrorCode.TooManyRequests,
        _ => ClientErrorCode.Unknown
    };

    private sealed record RefreshTokenRequest(string RefreshToken);
}

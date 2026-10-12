using System.Net;
using System.Text.Json;
using HouseholdFinances.Frontend.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace HouseholdFinances.Frontend.Tests.Services;

/// <summary>
/// Tests for the shared API client (issue #24): bearer attachment, refresh-on-401 with a single
/// retry, refresh-failure handling, and problem+json error mapping. A stub HTTP handler stands in
/// for the backend, so none of these tests need a live server.
/// </summary>
public class ApiClientTests
{
    private const string BaseUrl = "http://localhost:5252";

    [Fact]
    public async Task GetAsync_WithStoredToken_AttachesBearerToken()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{}");

        var store = new InMemoryTokenStore();
        await store.SetAsync(NewPair("access-1", "refresh-1"));

        var client = CreateClient(handler, store);

        await client.GetAsync<JsonElement>("/api/v1/users/me");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v1/users/me", request.Path);
        Assert.Equal("Bearer access-1", request.Authorization);
    }

    [Fact]
    public async Task GetAsync_WithoutStoredToken_SendsNoAuthorizationHeader()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{}");

        var client = CreateClient(handler, new InMemoryTokenStore());

        await client.GetAsync<JsonElement>("/api/v1/users/me");

        Assert.Null(Assert.Single(handler.Requests).Authorization);
    }

    [Fact]
    public async Task GetAsync_OnUnauthorized_RefreshesOnceReplacesTokensAndRetries()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Unauthorized, ProblemJson(3, "1", HttpStatusCode.Unauthorized));
        handler.EnqueueJson(HttpStatusCode.OK, TokenJson("access-2", "refresh-2"));
        handler.EnqueueJson(HttpStatusCode.OK, "{}");

        var store = new InMemoryTokenStore();
        await store.SetAsync(NewPair("access-1", "refresh-1"));

        var client = CreateClient(handler, store);

        await client.GetAsync<JsonElement>("/api/v1/users/me");

        Assert.Equal(3, handler.Requests.Count);
        Assert.Equal(1, handler.RefreshRequestCount);

        // The original request carried the original access token.
        Assert.Equal("Bearer access-1", handler.Requests[0].Authorization);

        // Exactly one refresh, sent anonymously with the stored refresh token.
        var refresh = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, refresh.Method);
        Assert.Equal("/api/v1/auth/refresh", refresh.Path);
        Assert.Null(refresh.Authorization);
        Assert.Equal("{\"refreshToken\":\"refresh-1\"}", refresh.Body);

        // The original request was retried once with the replacement access token.
        Assert.Equal(HttpMethod.Get, handler.Requests[2].Method);
        Assert.Equal("/api/v1/users/me", handler.Requests[2].Path);
        Assert.Equal("Bearer access-2", handler.Requests[2].Authorization);

        // Both tokens were replaced together.
        var stored = await store.GetAsync();
        Assert.NotNull(stored);
        Assert.Equal("access-2", stored!.AccessToken);
        Assert.Equal("refresh-2", stored.RefreshToken);
    }

    [Fact]
    public async Task GetAsync_OnUnauthorizedWithoutRefreshToken_DoesNotRefreshAndReportsUnauthorized()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Unauthorized, ProblemJson(3, "7", HttpStatusCode.Unauthorized));

        var store = new InMemoryTokenStore();
        await store.SetAsync(new TokenPair("access-1", FutureAccessExpiry, string.Empty, FutureRefreshExpiry));

        var client = CreateClient(handler, store);

        var exception = await Assert.ThrowsAsync<ClientErrorException>(
            () => client.GetAsync<JsonElement>("/api/v1/users/me"));

        Assert.Equal(ClientErrorCode.Unauthorized, exception.ErrorCode);
        Assert.Equal("7", exception.Identifier);
        Assert.Equal("Unauthorized 7", exception.Message);
        Assert.Single(handler.Requests);
        Assert.Equal(0, handler.RefreshRequestCount);
    }

    [Fact]
    public async Task GetAsync_OnUnauthorized_WhenRefreshFails_ClearsTokensAndReportsUnauthorized()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Unauthorized, ProblemJson(3, "1", HttpStatusCode.Unauthorized));
        handler.EnqueueJson(HttpStatusCode.Unauthorized, ProblemJson(3, "refresh", HttpStatusCode.Unauthorized));

        var store = new InMemoryTokenStore();
        await store.SetAsync(NewPair("access-1", "refresh-1"));

        var client = CreateClient(handler, store);

        var exception = await Assert.ThrowsAsync<ClientErrorException>(
            () => client.GetAsync<JsonElement>("/api/v1/users/me"));

        Assert.Equal(ClientErrorCode.Unauthorized, exception.ErrorCode);
        Assert.Equal(401, Assert.IsType<int>(exception.Identifier));
        Assert.Equal(1, handler.RefreshRequestCount);

        // The failed refresh gives up cleanly and does not retry the original request.
        Assert.Equal(2, handler.Requests.Count);
        Assert.Null(await store.GetAsync());
    }

    [Theory]
    [InlineData(1, 404, ClientErrorCode.NotFound)]
    [InlineData(2, 400, ClientErrorCode.InvalidInput)]
    [InlineData(3, 401, ClientErrorCode.Unauthorized)]
    [InlineData(4, 409, ClientErrorCode.Conflict)]
    [InlineData(5, 429, ClientErrorCode.TooManyRequests)]
    [InlineData(0, 500, ClientErrorCode.Unknown)]
    [InlineData(99, 418, ClientErrorCode.Unknown)]
    public async Task GetAsync_ProblemJson_MapsErrorCodeAndIdentifier(
        int numericErrorCode,
        int statusCode,
        ClientErrorCode expected)
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson((HttpStatusCode)statusCode, ProblemJson(numericErrorCode, "42", (HttpStatusCode)statusCode));

        var client = CreateClient(handler, new InMemoryTokenStore());

        var exception = await Assert.ThrowsAsync<ClientErrorException>(
            () => client.GetAsync<JsonElement>("/api/v1/things/42"));

        Assert.Equal(expected, exception.ErrorCode);
        Assert.Equal("42", exception.Identifier);
        Assert.Equal($"{expected} 42", exception.Message);
    }

    [Theory]
    [InlineData("Not json at all", "text/html")]
    [InlineData("{\"message\":\"this body has no errorCode\"}", "application/problem+json")]
    [InlineData("", "application/problem+json")]
    public async Task GetAsync_NonConformingErrorBody_MapsToUnknownWithHttpStatus(string body, string mediaType)
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.NotFound, body, mediaType);

        var client = CreateClient(handler, new InMemoryTokenStore());

        var exception = await Assert.ThrowsAsync<ClientErrorException>(
            () => client.GetAsync<JsonElement>("/api/v1/things/99"));

        Assert.Equal(ClientErrorCode.Unknown, exception.ErrorCode);
        Assert.Equal(404, Assert.IsType<int>(exception.Identifier));
        Assert.Equal("Unknown 404", exception.Message);
    }

    [Fact]
    public async Task PostAnonymousAsync_WithStoredTokens_SendsNoBearerTokenAndDoesNotRefresh()
    {
        var handler = new StubHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Unauthorized, ProblemJson(3, "google", HttpStatusCode.Unauthorized));

        var store = new InMemoryTokenStore();
        await store.SetAsync(NewPair("access-1", "refresh-1"));

        var client = CreateClient(handler, store);

        var exception = await Assert.ThrowsAsync<ClientErrorException>(
            () => client.PostAnonymousAsync<object, JsonElement>(
                "/api/v1/auth/google",
                new { idToken = "google-token" }));

        Assert.Equal(ClientErrorCode.Unauthorized, exception.ErrorCode);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Null(request.Authorization);
        Assert.Equal("{\"idToken\":\"google-token\"}", request.Body);
        Assert.Equal(0, handler.RefreshRequestCount);

        // Anonymous calls never touch the stored token pair.
        Assert.NotNull(await store.GetAsync());
    }

    [Fact]
    public async Task GetAsync_WhenApiIsUnreachable_ReportsUnknownInsteadOfLeakingTheRawException()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("connection refused"));

        var client = CreateClient(handler, new InMemoryTokenStore());

        var exception = await Assert.ThrowsAsync<ClientErrorException>(
            () => client.GetAsync<JsonElement>("/api/v1/users/me"));

        Assert.Equal(ClientErrorCode.Unknown, exception.ErrorCode);
        Assert.Equal("/api/v1/users/me", exception.Identifier);
        Assert.IsType<HttpRequestException>(exception.InnerException);
    }

    private static ApiClient CreateClient(HttpMessageHandler handler, ITokenStore tokenStore) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) },
            tokenStore,
            NullLogger<ApiClient>.Instance);

    private static TokenPair NewPair(string accessToken, string refreshToken) =>
        new(accessToken, FutureAccessExpiry, refreshToken, FutureRefreshExpiry);

    private static DateTime FutureAccessExpiry => DateTime.UtcNow.AddMinutes(5);

    private static DateTime FutureRefreshExpiry => DateTime.UtcNow.AddHours(1);

    private static string TokenJson(string accessToken, string refreshToken) =>
        JsonSerializer.Serialize(
            new
            {
                AccessToken = accessToken,
                AccessTokenExpiresAtUtc = FutureAccessExpiry,
                RefreshToken = refreshToken,
                RefreshTokenExpiresAtUtc = FutureRefreshExpiry,
                TokenType = "Bearer"
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static string ProblemJson(int errorCode, string identifier, HttpStatusCode status) =>
        JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["type"] = "about:blank",
            ["title"] = "Error",
            ["status"] = (int)status,
            ["errorCode"] = errorCode,
            ["errorName"] = "Error",
            ["identifier"] = identifier
        });

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHttpMessageHandler(Exception exception) => _exception = exception;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(_exception);
    }
}

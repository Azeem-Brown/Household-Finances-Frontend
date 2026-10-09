using System.Net;
using System.Text.Json;
using HouseholdFinances.Frontend.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace HouseholdFinances.Frontend.Tests.Services;

/// <summary>
/// Tests for the real Google sign-in service (issue #26). The browser Google Identity Services
/// boundary is a test double and the backend is a stub HTTP handler, so no live Google call or
/// server is needed. They cover the successful exchange (stored tokens plus the setup versus
/// dashboard routing decision) and the failure mapping (missing client id, canceled prompt,
/// interop failure, API errors, and an unreachable API).
/// </summary>
public class GoogleAuthenticationServiceTests
{
    private const string ClientId = "frontend-client-id.apps.googleusercontent.com";
    private const string BaseUrl = "http://localhost:5252";
    private const string GoogleIdToken = "google-id-token";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SignIn_WithoutHouseholds_StoresTokensAndRoutesToSetup()
    {
        var (service, handler, store, provider) = CreateService();
        handler.EnqueueJson(HttpStatusCode.OK, TokenJson("access-1", "refresh-1"));
        handler.EnqueueJson(HttpStatusCode.OK, ProfileJson(households: Array.Empty<object>()));

        var result = await service.SignInAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(AuthenticationDestination.Setup, result.Destination);
        Assert.Null(result.ErrorMessage);

        // The Google credential is exchanged anonymously, then the profile is read with the bearer.
        Assert.Equal(2, handler.Requests.Count);

        var exchange = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, exchange.Method);
        Assert.Equal(GoogleAuthenticationService.GoogleSignInPath, exchange.Path);
        Assert.Null(exchange.Authorization);
        Assert.Equal("{\"idToken\":\"google-id-token\"}", exchange.Body);

        var profile = handler.Requests[1];
        Assert.Equal(HttpMethod.Get, profile.Method);
        Assert.Equal(GoogleAuthenticationService.CurrentUserPath, profile.Path);
        Assert.Equal("Bearer access-1", profile.Authorization);

        // The API-issued pair was stored through the token store seam.
        var stored = await store.GetAsync();
        Assert.NotNull(stored);
        Assert.Equal("access-1", stored!.AccessToken);
        Assert.Equal("refresh-1", stored.RefreshToken);

        // The configured client id was handed to the browser boundary.
        Assert.Equal(ClientId, provider.ClientId);
    }

    [Fact]
    public async Task SignIn_WithHousehold_RoutesToDashboard()
    {
        var (service, handler, _, _) = CreateService();
        handler.EnqueueJson(HttpStatusCode.OK, TokenJson("access-1", "refresh-1"));
        handler.EnqueueJson(
            HttpStatusCode.OK,
            ProfileJson(new { Id = Guid.NewGuid(), Name = "Our Home" }));

        var result = await service.SignInAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(AuthenticationDestination.Dashboard, result.Destination);
    }

    [Fact]
    public async Task SignIn_WhenExchangeFails_ReportsApiErrorAndStoresNoTokens()
    {
        var (service, handler, store, _) = CreateService();
        handler.EnqueueJson(
            HttpStatusCode.Unauthorized,
            ProblemJson(3, "GoogleToken", HttpStatusCode.Unauthorized));

        var result = await service.SignInAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(ClientErrorCode.Unauthorized, result.ErrorCode);
        Assert.Equal("GoogleToken", result.Identifier);
        Assert.Equal("Unauthorized GoogleToken", result.ErrorMessage);

        // The exchange failed, so no profile call was attempted and nothing was stored.
        Assert.Single(handler.Requests);
        Assert.Null(await store.GetAsync());
    }

    [Fact]
    public async Task SignIn_WhenPromptCanceled_ReportsFailureWithoutCallingTheApi()
    {
        var (service, handler, _, provider) = CreateService();
        provider.IdToken = null;

        var result = await service.SignInAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(ClientErrorCode.Unauthorized, result.ErrorCode);
        Assert.Equal("Unauthorized GoogleSignInCanceled", result.ErrorMessage);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SignIn_WhenClientIdMissing_ReportsClearFailureWithoutInteropOrHttp()
    {
        var (service, handler, _, provider) = CreateService(clientId: "   ");

        var result = await service.SignInAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(ClientErrorCode.InvalidInput, result.ErrorCode);
        Assert.Equal("InvalidInput GoogleClientId", result.ErrorMessage);
        Assert.Equal(0, provider.CallCount);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SignIn_WhenInteropFails_ReportsFailureWithoutCallingTheApi()
    {
        var (service, handler, _, provider) = CreateService();
        provider.Throw = new JSException("Google Identity Services failed to load.");

        var result = await service.SignInAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(ClientErrorCode.Unknown, result.ErrorCode);
        Assert.Equal("Unknown GoogleIdentityUnavailable", result.ErrorMessage);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SignIn_WhenApiUnreachable_ReportsCleanFailure()
    {
        var service = BuildService(
            BuildConfiguration(ClientId),
            new ThrowingHttpMessageHandler(new HttpRequestException("connection refused")),
            new InMemoryTokenStore(),
            new StubGoogleIdentityProvider { IdToken = GoogleIdToken });

        var result = await service.SignInAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(ClientErrorCode.Unknown, result.ErrorCode);
        Assert.Equal(GoogleAuthenticationService.GoogleSignInPath, result.Identifier);
    }

    [Fact]
    public async Task SignIn_WhenProfileReadFails_ReportsApiError()
    {
        var (service, handler, _, _) = CreateService();
        handler.EnqueueJson(HttpStatusCode.OK, TokenJson("access-1", "refresh-1"));
        handler.EnqueueJson(HttpStatusCode.NotFound, ProblemJson(1, "user", HttpStatusCode.NotFound));

        var result = await service.SignInAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(ClientErrorCode.NotFound, result.ErrorCode);
        Assert.Equal("user", result.Identifier);
    }

    private static (GoogleAuthenticationService Service, StubHttpMessageHandler Handler, InMemoryTokenStore Store, StubGoogleIdentityProvider Provider)
        CreateService(string? clientId = ClientId)
    {
        var handler = new StubHttpMessageHandler();
        var store = new InMemoryTokenStore();
        var provider = new StubGoogleIdentityProvider { IdToken = GoogleIdToken };

        var service = BuildService(BuildConfiguration(clientId), handler, store, provider);

        return (service, handler, store, provider);
    }

    private static GoogleAuthenticationService BuildService(
        IConfiguration configuration,
        HttpMessageHandler handler,
        InMemoryTokenStore store,
        StubGoogleIdentityProvider provider)
    {
        var apiClient = new ApiClient(
            new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) },
            store,
            NullLogger<ApiClient>.Instance);

        return new GoogleAuthenticationService(
            configuration,
            provider,
            apiClient,
            store,
            NullLogger<GoogleAuthenticationService>.Instance);
    }

    private static IConfiguration BuildConfiguration(string? clientId) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Google:ClientId"] = clientId
            })
            .Build();

    private static string TokenJson(string accessToken, string refreshToken) =>
        JsonSerializer.Serialize(
            new
            {
                AccessToken = accessToken,
                AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
                RefreshToken = refreshToken,
                RefreshTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
                TokenType = "Bearer"
            },
            WebJson);

    private static string ProfileJson(params object[] households) =>
        JsonSerializer.Serialize(
            new
            {
                Id = Guid.NewGuid(),
                Name = "Ada Lovelace",
                Email = "ada@example.com",
                Households = households
            },
            WebJson);

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

using HouseholdFinances.Frontend.Models;
using Microsoft.JSInterop;

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// The real <see cref="IAuthenticationService"/>: it obtains a Google ID token in the browser,
/// exchanges it for the API-issued token pair through the shared API client, stores the pair, and
/// decides the post-login destination from the user's household membership.
/// </summary>
/// <remarks>
/// The Google ID token is obtained in the browser (Google Identity Services) and the exchange is
/// performed server to server, so the API-issued tokens never reach the browser. Every failure -
/// a missing client id, a canceled or unavailable Google prompt, or an API error - is reported as
/// an <see cref="AuthenticationResult.Failure"/> using the shared client error convention, so the
/// Login page never has to handle a raw exception. This class only writes through the configured
/// <see cref="ITokenStore"/>: session persistence across a browser reload or an app restart is
/// handled there (issue #28), so signing in carries no persistence logic of its own.
/// </remarks>
public sealed class GoogleAuthenticationService : IAuthenticationService
{
    /// <summary>The anonymous endpoint that exchanges a Google ID token for the API token pair.</summary>
    public const string GoogleSignInPath = "/api/v1/auth/google";

    /// <summary>The endpoint that returns the signed-in user's profile and household membership.</summary>
    public const string CurrentUserPath = "/api/v1/users/me";

    /// <summary>The configuration key holding the frontend Google OAuth client id.</summary>
    public const string ClientIdConfigurationKey = "Authentication:Google:ClientId";

    /// <summary>Identifier shown when the Google client id is not configured.</summary>
    public const string ClientIdIdentifier = "GoogleClientId";

    /// <summary>Identifier shown when the user cancels or cannot complete the Google prompt.</summary>
    public const string SignInCanceledIdentifier = "GoogleSignInCanceled";

    /// <summary>Identifier shown when Google Identity Services could not be reached in the browser.</summary>
    public const string IdentityUnavailableIdentifier = "GoogleIdentityUnavailable";

    private readonly IConfiguration _configuration;
    private readonly IGoogleIdentityProvider _googleIdentityProvider;
    private readonly IApiClient _apiClient;
    private readonly ITokenStore _tokenStore;
    private readonly ILogger<GoogleAuthenticationService> _logger;

    /// <summary>Creates the service.</summary>
    /// <param name="configuration">The configuration holding the Google OAuth client id.</param>
    /// <param name="googleIdentityProvider">The browser boundary that returns the Google ID token.</param>
    /// <param name="apiClient">The shared API client used for the exchange and profile read.</param>
    /// <param name="tokenStore">The store that keeps the API-issued token pair.</param>
    /// <param name="logger">The logger used to record sign-in failures.</param>
    public GoogleAuthenticationService(
        IConfiguration configuration,
        IGoogleIdentityProvider googleIdentityProvider,
        IApiClient apiClient,
        ITokenStore tokenStore,
        ILogger<GoogleAuthenticationService> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _googleIdentityProvider = googleIdentityProvider ?? throw new ArgumentNullException(nameof(googleIdentityProvider));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<AuthenticationResult> SignInAsync(CancellationToken cancellationToken = default)
    {
        // 1. The client id is configuration (not a secret) and must be configured before the
        //    browser flow can start. Report it clearly instead of letting interop fail.
        var clientId = _configuration[ClientIdConfigurationKey];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogError(
                "The Google client id '{ConfigurationKey}' is not configured.",
                ClientIdConfigurationKey);

            return AuthenticationResult.Failure(ClientErrorCode.InvalidInput, ClientIdIdentifier);
        }

        // 2. Obtain the Google ID token in the browser. A null result means the user canceled or
        //    the prompt was unavailable; a JSException means the interop itself failed.
        string? idToken;
        try
        {
            idToken = await _googleIdentityProvider.GetIdTokenAsync(clientId, cancellationToken);
        }
        catch (JSException exception)
        {
            _logger.LogWarning(exception, "The Google Identity Services sign-in could not be completed.");

            return AuthenticationResult.Failure(ClientErrorCode.Unknown, IdentityUnavailableIdentifier);
        }

        if (string.IsNullOrEmpty(idToken))
        {
            return AuthenticationResult.Failure(ClientErrorCode.Unauthorized, SignInCanceledIdentifier);
        }

        // 3. Exchange the Google ID token for the API token pair and store it, then read the
        //    profile that decides the destination. The API client already translates every HTTP or
        //    transport failure into a ClientErrorException, so map that onto the shared convention.
        UserProfile profile;
        try
        {
            var tokens = await _apiClient.PostAnonymousAsync<GoogleSignInRequest, TokenPair>(
                GoogleSignInPath,
                new GoogleSignInRequest(idToken),
                cancellationToken);

            await _tokenStore.SetAsync(tokens, cancellationToken);

            profile = await _apiClient.GetAsync<UserProfile>(CurrentUserPath, cancellationToken);
        }
        catch (ClientErrorException exception)
        {
            _logger.LogWarning(
                exception,
                "Sign-in failed while exchanging the Google token or loading the current user profile.");

            return AuthenticationResult.Failure(exception.ErrorCode, exception.Identifier);
        }

        // 4. No household membership means the user must complete setup first.
        return AuthenticationResult.Success(profile.Households.Count == 0
            ? AuthenticationDestination.Setup
            : AuthenticationDestination.Dashboard);
    }

    /// <inheritdoc />
    public async Task<bool> HasSessionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _tokenStore.GetAsync(cancellationToken) is not null;
        }
        catch (Exception exception)
        {
            // The shell asks this on every navigation, so a store that cannot answer must look
            // signed out instead of failing to render the app bar.
            _logger.LogWarning(exception, "A session could not be read and was treated as signed out.");
            return false;
        }
    }

    /// <inheritdoc />
    public Task SignOutAsync(CancellationToken cancellationToken = default) =>
        _tokenStore.ClearAsync(cancellationToken);
        
    public async Task<AuthenticationDestination?> TryResumeAsync(CancellationToken cancellationToken = default)
    {
        TokenPair? tokens;
        try
        {
            tokens = await _tokenStore.GetAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // This is a probe, not a sign-in: a store that cannot answer must look signed out so the
            // page falls back to the sign-in flow rather than failing to render.
            _logger.LogWarning(exception, "A stored session could not be read and was treated as signed out.");
            return null;
        }

        if (tokens is null)
        {
            return null;
        }

        UserProfile profile;
        try
        {
            profile = await _apiClient.GetAsync<UserProfile>(CurrentUserPath, cancellationToken);
        }
        catch (ClientErrorException exception)
        {
            // A stored pair the API will not accept is worse than no session: clear it so the user
            // is shown the sign-in button instead of a page that keeps failing every request.
            _logger.LogWarning(exception, "A stored session could not be resumed and was cleared.");

            await _tokenStore.ClearAsync(cancellationToken);
            return null;
        }

        return profile.Households.Count == 0
            ? AuthenticationDestination.Setup
            : AuthenticationDestination.Dashboard;
    }

    /// <summary>The anonymous request body for <c>POST /api/v1/auth/google</c>.</summary>
    private sealed record GoogleSignInRequest(string IdToken);
}

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// The browser-side boundary that obtains a Google ID token through Google Identity Services.
/// </summary>
/// <remarks>
/// The Google ID token can only be obtained in the browser, so this is a JavaScript interop
/// boundary: the Blazor Server host loads Google Identity Services in the user's browser and hands
/// the resulting credential back to .NET, which then performs the token exchange server to server.
/// Keeping this a small interface lets the sign-in flow be tested without a browser or any real
/// Google credentials.
/// </remarks>
public interface IGoogleIdentityProvider
{
    /// <summary>
    /// Prompts the user to sign in with Google and returns the resulting ID token.
    /// </summary>
    /// <param name="clientId">
    /// The Google OAuth client id to initialize Google Identity Services with; it must match the
    /// client id the backend validates the token audience against.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the interaction.</param>
    /// <returns>
    /// The Google ID token (the credential) when the user completes sign-in, or <see langword="null"/>
    /// when the user cancels or the prompt is not displayed.
    /// </returns>
    /// <exception cref="Microsoft.JSInterop.JSException">
    /// The Google Identity Services script or module could not be loaded, or the browser interaction
    /// failed. This represents an unavailable prompt, reported by the caller through the shared
    /// client error convention.
    /// </exception>
    Task<string?> GetIdTokenAsync(string clientId, CancellationToken cancellationToken = default);
}

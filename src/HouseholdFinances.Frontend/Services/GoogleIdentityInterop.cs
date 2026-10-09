using Microsoft.JSInterop;

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// The default <see cref="IGoogleIdentityProvider"/>. It imports the
/// <c>wwwroot/js/google-identity.js</c> interop module and invokes it to run Google Identity
/// Services in the browser and return the ID token.
/// </summary>
/// <remarks>
/// All of the browser-facing work lives in the JavaScript module; this class is only the thin,
/// testable boundary that loads the module and marshals the result. The module is loaded once per
/// circuit and disposed with the service.
/// </remarks>
public sealed class GoogleIdentityInterop : IGoogleIdentityProvider, IAsyncDisposable
{
    /// <summary>The path of the interop module, relative to the app's base URL.</summary>
    public const string ModulePath = "./js/google-identity.js";

    /// <summary>The exported module function that returns the Google ID token.</summary>
    public const string GetIdTokenFunction = "getGoogleIdToken";

    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    /// <summary>Creates the provider.</summary>
    /// <param name="jsRuntime">The JavaScript runtime used to reach Google Identity Services.</param>
    public GoogleIdentityInterop(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    /// <inheritdoc />
    public async Task<string?> GetIdTokenAsync(string clientId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        var module = await GetModuleAsync(cancellationToken);
        return await module.InvokeAsync<string?>(GetIdTokenFunction, cancellationToken, clientId);
    }

    private async Task<IJSObjectReference> GetModuleAsync(CancellationToken cancellationToken) =>
        _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
            _module = null;
        }
    }
}

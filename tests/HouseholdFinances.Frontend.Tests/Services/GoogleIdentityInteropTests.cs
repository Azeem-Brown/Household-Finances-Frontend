using Bunit;
using HouseholdFinances.Frontend.Services;

namespace HouseholdFinances.Frontend.Tests.Services;

/// <summary>
/// Tests for the JavaScript interop boundary (issue #26, updated by issue #31) using bUnit's JS
/// interop double, so the module invocation is verified without a browser or a live Google Identity
/// Services script. The module itself renders Google's button in the browser and cannot run here;
/// these tests only prove the .NET boundary passes the client id and marshals the credential.
/// </summary>
public class GoogleIdentityInteropTests : BunitContext
{
    [Fact]
    public async Task GetIdToken_InvokesModuleFunctionWithClientIdAndReturnsCredential()
    {
        var module = JSInterop.SetupModule(GoogleIdentityInterop.ModulePath);
        module
            .Setup<string>(GoogleIdentityInterop.GetIdTokenFunction, "client-1")
            .SetResult("google-id-token");

        var provider = new GoogleIdentityInterop(JSInterop.JSRuntime);

        var token = await provider.GetIdTokenAsync("client-1");

        Assert.Equal("google-id-token", token);
    }

    [Fact]
    public async Task GetIdToken_WhenNoCredentialIsReturned_ReturnsNull()
    {
        var module = JSInterop.SetupModule(GoogleIdentityInterop.ModulePath);
        module
            .Setup<string>(GoogleIdentityInterop.GetIdTokenFunction, "client-1")
            .SetResult(null!);

        var provider = new GoogleIdentityInterop(JSInterop.JSRuntime);

        var token = await provider.GetIdTokenAsync("client-1");

        Assert.Null(token);
    }
}

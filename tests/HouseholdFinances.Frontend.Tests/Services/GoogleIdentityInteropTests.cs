using Bunit;
using HouseholdFinances.Frontend.Services;

namespace HouseholdFinances.Frontend.Tests.Services;

/// <summary>
/// Tests for the JavaScript interop boundary (issue #26) using bUnit's JS interop double, so the
/// module invocation is verified without a browser or a live Google Identity Services script.
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
    public async Task GetIdToken_WhenPromptReturnsNoCredential_ReturnsNull()
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

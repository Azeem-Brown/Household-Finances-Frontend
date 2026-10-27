using Bunit;
using Bunit.TestDoubles;
using HouseholdFinances.Frontend.Components.Pages;
using HouseholdFinances.Frontend.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace HouseholdFinances.Frontend.Tests.Components;

/// <summary>
/// bUnit component tests for the Login page (issue #9, updated by issue #31): render, sign-in
/// action, failure display through the shared client error convention, and post-login routing.
/// </summary>
/// <remarks>
/// Google's rendered button is produced by JavaScript in the browser, which cannot run in unit
/// tests, so these tests stand in for the authentication service with a test double. They prove the
/// page hosts the button container, starts the sign-in flow once so the rendered button works,
/// surfaces every failure as a visible error, and routes on success. They do not prove that the
/// browser renders the button or that a real Google sign-in completes.
/// </remarks>
public class LoginPageTests : BunitContext
{
    private readonly StubAuthenticationService _authenticationService = new();
    private readonly BunitNavigationManager _navigation;

    public LoginPageTests()
    {
        // MudBlazor components may touch JS interop; loose mode keeps render side-effect free.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IAuthenticationService>(_authenticationService);

        _navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
    }

    [Fact]
    public void Login_RendersTitleAndGoogleButtonContainer()
    {
        var cut = Render<Login>();

        Assert.Equal("Login", cut.Find("h4").TextContent.Trim());

        // The JavaScript module renders Google's button into this container, so the container must
        // be present for the button to appear.
        var container = cut.Find("#google-signin-button");
        Assert.Equal("signin", container.GetAttribute("data-testid"));
    }

    [Fact]
    public void Login_StartsTheSignInFlowOnceForTheRenderedButton()
    {
        var cut = Render<Login>();

        // The flow must be in flight so the rendered button can start sign-in when clicked.
        cut.WaitForAssertion(() => Assert.Equal(1, _authenticationService.CallCount));
    }

    [Fact]
    public void SignIn_Failure_ShowsConventionMessageAndDoesNotNavigate()
    {
        _authenticationService.Result =
            AuthenticationResult.Failure(ClientErrorCode.Unauthorized, 7);

        var cut = Render<Login>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[data-testid='login-error']");
            Assert.Equal("Unauthorized 7", alert.TextContent.Trim());
            Assert.Equal("http://localhost/", _navigation.Uri);
        });

        Assert.Equal(1, _authenticationService.CallCount);
    }

    [Fact]
    public void SignIn_ClientErrorException_ShowsItsConventionMessage()
    {
        _authenticationService.Throw = new ClientErrorException(ClientErrorCode.Conflict, "household");

        var cut = Render<Login>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[data-testid='login-error']");
            Assert.Equal("Conflict household", alert.TextContent.Trim());
            Assert.Equal("http://localhost/", _navigation.Uri);
        });
    }

    [Fact]
    public void SignIn_UnexpectedException_ShowsConventionMessageAndDoesNotNavigate()
    {
        _authenticationService.Throw = new InvalidOperationException("boom");

        var cut = Render<Login>();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[data-testid='login-error']");
            Assert.Equal("Unknown InvalidOperationException", alert.TextContent.Trim());
            Assert.Equal("http://localhost/", _navigation.Uri);
        });
    }

    [Fact]
    public void SignIn_SuccessWithoutHousehold_NavigatesToSetup()
    {
        _authenticationService.Result =
            AuthenticationResult.Success(AuthenticationDestination.Setup);

        var cut = Render<Login>();

        cut.WaitForAssertion(() =>
            Assert.EndsWith("/setup", _navigation.Uri, StringComparison.Ordinal));
        Assert.Empty(cut.FindAll("[data-testid='login-error']"));
    }

    [Fact]
    public void SignIn_SuccessWithHousehold_NavigatesToTheHouseholdList()
    {
        _authenticationService.Result =
            AuthenticationResult.Success(AuthenticationDestination.Dashboard);

        var cut = Render<Login>();

        cut.WaitForAssertion(() =>
            Assert.EndsWith("/households", _navigation.Uri, StringComparison.Ordinal));
        Assert.Empty(cut.FindAll("[data-testid='login-error']"));
    }

    [Fact]
    public void Login_WithExistingSessionWithHousehold_NavigatesToTheHouseholdListWithoutSigningIn()
    {
        _authenticationService.ResumeDestination = AuthenticationDestination.Dashboard;

        var cut = Render<Login>();

        cut.WaitForAssertion(() =>
            Assert.EndsWith("/households", _navigation.Uri, StringComparison.Ordinal));
        Assert.Equal(1, _authenticationService.ResumeCallCount);
        Assert.Equal(0, _authenticationService.CallCount);
    }

    [Fact]
    public void Login_WithExistingSessionWithoutHousehold_NavigatesToSetupWithoutSigningIn()
    {
        _authenticationService.ResumeDestination = AuthenticationDestination.Setup;

        var cut = Render<Login>();

        cut.WaitForAssertion(() =>
            Assert.EndsWith("/setup", _navigation.Uri, StringComparison.Ordinal));
        Assert.Equal(1, _authenticationService.ResumeCallCount);
        Assert.Equal(0, _authenticationService.CallCount);
    }

    [Fact]
    public void Login_WithoutSession_RendersTheSignInFlowAndDoesNotNavigate()
    {
        _authenticationService.ResumeDestination = null;

        var cut = Render<Login>();

        cut.WaitForAssertion(() => Assert.Equal(1, _authenticationService.CallCount));
        Assert.Equal(1, _authenticationService.ResumeCallCount);
        Assert.Equal("http://localhost/", _navigation.Uri);
    }
}

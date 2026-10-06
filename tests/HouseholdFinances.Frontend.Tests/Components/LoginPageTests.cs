using Bunit;
using Bunit.TestDoubles;
using HouseholdFinances.Frontend.Components.Pages;
using HouseholdFinances.Frontend.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace HouseholdFinances.Frontend.Tests.Components;

/// <summary>
/// bUnit component tests for the Login page (issue #9): render, sign-in action, failure display
/// through the shared client error convention, and post-login routing.
/// </summary>
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
    public void Login_RendersTitleAndSignInAction()
    {
        var cut = Render<Login>();

        Assert.Equal("Login", cut.Find("h4").TextContent.Trim());

        var button = cut.Find("[data-testid='signin'] button");
        Assert.Contains("Sign in with Google", button.TextContent);
        Assert.False(button.HasAttribute("disabled"));
    }

    [Fact]
    public async Task SignIn_Failure_ShowsConventionMessageAndDoesNotNavigate()
    {
        _authenticationService.Result =
            AuthenticationResult.Failure(ClientErrorCode.Unauthorized, 7);

        var cut = Render<Login>();

        await cut.Find("[data-testid='signin'] button").ClickAsync();

        var alert = cut.Find("[data-testid='login-error']");
        Assert.Equal("Unauthorized 7", alert.TextContent.Trim());
        Assert.Equal("http://localhost/", _navigation.Uri);
        Assert.Equal(1, _authenticationService.CallCount);
    }

    [Fact]
    public async Task SignIn_SuccessWithoutHousehold_NavigatesToSetup()
    {
        _authenticationService.Result =
            AuthenticationResult.Success(AuthenticationDestination.Setup);

        var cut = Render<Login>();

        await cut.Find("[data-testid='signin'] button").ClickAsync();

        Assert.EndsWith("/setup", _navigation.Uri, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid='login-error']"));
    }

    [Fact]
    public async Task SignIn_SuccessWithHousehold_NavigatesToDashboard()
    {
        _authenticationService.Result =
            AuthenticationResult.Success(AuthenticationDestination.Dashboard);

        var cut = Render<Login>();

        await cut.Find("[data-testid='signin'] button").ClickAsync();

        Assert.EndsWith("/dashboard", _navigation.Uri, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid='login-error']"));
    }
}

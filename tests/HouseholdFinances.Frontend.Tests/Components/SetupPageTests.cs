using System.Text.Json;
using Bunit;
using Bunit.TestDoubles;
using HouseholdFinances.Frontend.Components.Pages;
using HouseholdFinances.Frontend.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace HouseholdFinances.Frontend.Tests.Components;

/// <summary>
/// bUnit component tests for the Setup page (issue #11): client-side validation of the household
/// name, the success path that creates the household and routes to the dashboard, and the failure
/// path that shows the shared error convention without navigating.
/// </summary>
/// <remarks>
/// The API client is replaced with a test double, so no live backend or browser is involved. The
/// tests prove the page rejects an invalid name before any request, sends the expected request on
/// success, and surfaces every failure as a visible message; they do not prove the real backend
/// accepts the request or that the browser renders the page.
/// </remarks>
public class SetupPageTests : BunitContext
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly StubApiClient _apiClient = new();
    private readonly BunitNavigationManager _navigation;

    public SetupPageTests()
    {
        // MudBlazor components may touch JS interop; loose mode keeps render side-effect free.
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<IApiClient>(_apiClient);

        _navigation = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
    }

    [Fact]
    public void Setup_RendersTitleAndNameField()
    {
        var cut = Render<Setup>();

        Assert.Equal("Setup", cut.Find("h4").TextContent.Trim());
        Assert.Single(cut.FindAll("input"));
        Assert.Empty(cut.FindAll("[data-testid='setup-error']"));
    }

    [Fact]
    public void Submit_WithEmptyName_ShowsValidationMessageAndDoesNotCallApi()
    {
        var cut = Render<Setup>();

        cut.Find("[data-testid='setup-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(Setup.NameRequiredMessage, cut.Markup);
            Assert.Equal("http://localhost/", _navigation.Uri);
        });
        Assert.Empty(_apiClient.Posts);
    }

    [Fact]
    public void Submit_WithWhitespaceName_ShowsValidationMessageAndDoesNotCallApi()
    {
        var cut = Render<Setup>();

        // MudTextField with Immediate="true" binds oninput, so the test must dispatch that event
        // rather than onchange, which the field does not handle.
        cut.Find("input").Input("   ");
        cut.Find("[data-testid='setup-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains(Setup.NameRequiredMessage, cut.Markup);
            Assert.Equal("http://localhost/", _navigation.Uri);
        });
        Assert.Empty(_apiClient.Posts);
    }

    [Fact]
    public void Submit_WithValidName_CreatesHouseholdAndNavigatesToDashboard()
    {
        var cut = Render<Setup>();

        // MudTextField with Immediate="true" binds oninput, so the test must dispatch that event
        // rather than onchange, which the field does not handle.
        cut.Find("input").Input("Family");
        cut.Find("[data-testid='setup-submit']").Click();

        var request = Assert.Single(_apiClient.Posts);
        Assert.Equal(Setup.HouseholdsPath, request.Path);
        Assert.Equal("{\"name\":\"Family\"}", JsonSerializer.Serialize(request.Body, WebJson));

        cut.WaitForAssertion(() =>
            Assert.EndsWith(Setup.DashboardPath, _navigation.Uri, StringComparison.Ordinal));
        Assert.Empty(cut.FindAll("[data-testid='setup-error']"));
    }

    [Fact]
    public void Submit_WithValidName_TrimsTheNameBeforeSending()
    {
        var cut = Render<Setup>();

        cut.Find("input").Input("  Family  ");
        cut.Find("[data-testid='setup-submit']").Click();

        var request = Assert.Single(_apiClient.Posts);
        Assert.Equal("{\"name\":\"Family\"}", JsonSerializer.Serialize(request.Body, WebJson));
    }

    [Fact]
    public void Submit_WhenApiReportsConflict_ShowsConventionMessageAndDoesNotNavigate()
    {
        _apiClient.Throw = new ClientErrorException(ClientErrorCode.Conflict, "household");

        var cut = Render<Setup>();

        cut.Find("input").Input("Family");
        cut.Find("[data-testid='setup-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[data-testid='setup-error']");
            Assert.Equal("Conflict household", alert.TextContent.Trim());
            Assert.Equal("http://localhost/", _navigation.Uri);
        });
        Assert.Single(_apiClient.Posts);
    }

    [Fact]
    public void Submit_WhenApiThrowsUnexpectedException_ShowsConventionMessageAndDoesNotNavigate()
    {
        _apiClient.Throw = new InvalidOperationException("boom");

        var cut = Render<Setup>();

        cut.Find("input").Input("Family");
        cut.Find("[data-testid='setup-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find("[data-testid='setup-error']");
            Assert.Equal("Unknown InvalidOperationException", alert.TextContent.Trim());
            Assert.Equal("http://localhost/", _navigation.Uri);
        });
    }
}

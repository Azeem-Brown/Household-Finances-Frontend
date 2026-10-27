using HouseholdFinances.Frontend.Models;
using HouseholdFinances.Frontend.Services;
using HouseholdFinances.Frontend.Tests.Components;

namespace HouseholdFinances.Frontend.Tests.Services;

/// <summary>
/// Verifies the access rule the shell uses: which state a user is in, which page each state belongs
/// on, and which pages it may open.
/// </summary>
/// <remarks>
/// The session and the API are test doubles, so these tests prove the rule itself; the shell tests
/// prove that the menu and the page gate follow it.
/// </remarks>
public class PageAccessServiceTests
{
    [Fact]
    public async Task ResolveAsync_WithoutASession_ReportsSignedOut()
    {
        var authentication = new StubAuthenticationService { HasSession = false };
        var service = new PageAccessService(authentication, new HouseholdContextService(new StubApiClient()));

        await service.ResolveAsync();

        Assert.Equal(AppAccessState.SignedOut, service.State);
        Assert.True(service.IsHouseholdStateKnown);
    }

    [Fact]
    public async Task ResolveAsync_WithASessionAndNoHousehold_ReportsNoHousehold()
    {
        var authentication = new StubAuthenticationService { HasSession = true };
        var api = new StubApiClient { GetResponse = new List<Household>() };
        var service = new PageAccessService(authentication, new HouseholdContextService(api));

        await service.ResolveAsync();

        Assert.Equal(AppAccessState.NoHousehold, service.State);
    }

    [Fact]
    public async Task ResolveAsync_WithASessionAndAHousehold_ReportsReady()
    {
        var authentication = new StubAuthenticationService { HasSession = true };
        var api = new StubApiClient
        {
            GetResponse = new List<Household> { new Household { Id = Guid.NewGuid(), Name = "Family" } }
        };
        var service = new PageAccessService(authentication, new HouseholdContextService(api));

        await service.ResolveAsync();

        Assert.Equal(AppAccessState.Ready, service.State);
    }

    [Fact]
    public async Task ResolveAsync_WhenTheHouseholdListFails_ReportsReadyButFlagsAnUnknownState()
    {
        var authentication = new StubAuthenticationService { HasSession = true };
        var api = new StubApiClient { Throw = new ClientErrorException(ClientErrorCode.Unknown, "households") };
        var service = new PageAccessService(authentication, new HouseholdContextService(api));

        await service.ResolveAsync();

        // Treating a failed load as "no household" would send the user to Setup and tell them to
        // create a household they may already have.
        Assert.Equal(AppAccessState.Ready, service.State);
        Assert.False(service.IsHouseholdStateKnown);
    }

    [Theory]
    [InlineData(AppAccessState.SignedOut, "/login")]
    [InlineData(AppAccessState.NoHousehold, "/setup")]
    [InlineData(AppAccessState.Ready, "/households")]
    public void RedirectTargetFor_ReturnsThePageOfEachState(AppAccessState state, string expected)
    {
        Assert.Equal(expected, PageAccessService.RedirectTargetFor(state));
    }

    [Theory]
    [InlineData(AppAccessState.SignedOut, "/login", true)]
    [InlineData(AppAccessState.SignedOut, "/dashboard", false)]
    [InlineData(AppAccessState.SignedOut, "/setup", false)]
    [InlineData(AppAccessState.SignedOut, "/not-found", true)]
    [InlineData(AppAccessState.NoHousehold, "/setup", true)]
    [InlineData(AppAccessState.NoHousehold, "/profile", true)]
    [InlineData(AppAccessState.NoHousehold, "/dashboard", false)]
    [InlineData(AppAccessState.NoHousehold, "/login", true)]
    [InlineData(AppAccessState.Ready, "/dashboard", true)]
    [InlineData(AppAccessState.Ready, "/", true)]
    [InlineData(AppAccessState.Ready, "/households", true)]
    [InlineData(AppAccessState.Ready, "/setup", false)]
    [InlineData(AppAccessState.Ready, "/login", true)]
    public void IsAllowed_FollowsTheState(AppAccessState state, string path, bool expected)
    {
        Assert.Equal(expected, PageAccessService.IsAllowed(state, path));
    }

    [Theory]
    [InlineData("", "/")]
    [InlineData("/", "/")]
    [InlineData("dashboard", "/dashboard")]
    [InlineData("/Dashboard/", "/dashboard")]
    [InlineData("/setup?x=1", "/setup")]
    [InlineData("/bills#top", "/bills")]
    public void Normalize_IgnoresTheQueryFragmentSlashAndCasing(string path, string expected)
    {
        Assert.Equal(expected, PageAccessService.Normalize(path));
    }

    [Fact]
    public void IsAllowed_IgnoresTheQueryStringOfAnAllowedPage()
    {
        Assert.True(PageAccessService.IsAllowed(AppAccessState.NoHousehold, "/setup?step=1"));
    }
}

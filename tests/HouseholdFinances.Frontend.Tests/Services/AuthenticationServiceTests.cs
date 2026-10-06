using HouseholdFinances.Frontend.Services;

namespace HouseholdFinances.Frontend.Tests.Services;

/// <summary>
/// Unit tests for the client-side authentication service surface (issue #9). The real Google
/// Identity implementation is delivered by backend issue #11; these tests pin the seam and the
/// shared client error convention used until then.
/// </summary>
public class AuthenticationServiceTests
{
    [Fact]
    public void Success_CarriesDestinationAndNoErrorMessage()
    {
        var result = AuthenticationResult.Success(AuthenticationDestination.Dashboard);

        Assert.True(result.Succeeded);
        Assert.Equal(AuthenticationDestination.Dashboard, result.Destination);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Failure_FormatsMessageWithConvention()
    {
        var result = AuthenticationResult.Failure(ClientErrorCode.Unauthorized, 7);

        Assert.False(result.Succeeded);
        Assert.Equal(ClientErrorCode.Unauthorized, result.ErrorCode);
        Assert.Equal(7, result.Identifier);
        Assert.Equal("Unauthorized 7", result.ErrorMessage);
    }

    [Fact]
    public void Failure_RejectsNullIdentifier()
    {
        Assert.Throws<ArgumentNullException>(
            () => AuthenticationResult.Failure(ClientErrorCode.Unknown, null!));
    }

    [Fact]
    public async Task UnavailableService_ReportsUnavailableWithoutPretendingToAuthenticate()
    {
        var service = new UnavailableAuthenticationService();

        var result = await service.SignInAsync();

        Assert.False(result.Succeeded);
        Assert.Equal(ClientErrorCode.Unknown, result.ErrorCode);
        Assert.Equal("Unknown AuthenticationUnavailable", result.ErrorMessage);
        Assert.Null(result.Destination);
    }
}

using HouseholdFinances.Frontend.Services;

namespace HouseholdFinances.Frontend.Tests.Services;

/// <summary>
/// Unit tests for the client-side authentication result surface (issue #9): the success and failure
/// shapes and the shared client error convention. The Google sign-in flow itself is covered by
/// <see cref="GoogleAuthenticationServiceTests"/>.
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
}

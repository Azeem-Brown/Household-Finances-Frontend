using HouseholdFinances.Frontend.Services;

namespace HouseholdFinances.Frontend.Tests.Services;

public class ClientErrorConventionTests
{
    [Fact]
    public void Format_PlacesEnumBeforeIdentifier()
    {
        // Representative enum value and numeric identifier of significance.
        var message = ClientErrorFormatter.Format(ClientErrorCode.NotFound, 42);

        Assert.Equal("NotFound 42", message);
        Assert.StartsWith("NotFound", message);
        Assert.EndsWith("42", message);
    }

    [Fact]
    public void Format_SupportsNonNumericIdentifiers()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var message = ClientErrorFormatter.Format(ClientErrorCode.Conflict, id);

        Assert.Equal($"Conflict {id}", message);
        Assert.Contains("Conflict", message);
        Assert.Contains(id.ToString(), message);
    }

    [Fact]
    public void Format_RejectsNullIdentifier()
    {
        Assert.Throws<ArgumentNullException>(
            () => ClientErrorFormatter.Format(ClientErrorCode.InvalidInput, null!));
    }

    [Fact]
    public void Exception_MessageContainsEnumAndIdentifier()
    {
        var exception = new ClientErrorException(ClientErrorCode.Unauthorized, 7);

        Assert.Equal(ClientErrorCode.Unauthorized, exception.ErrorCode);
        Assert.Equal(7, exception.Identifier);
        Assert.Equal("Unauthorized 7", exception.Message);
    }

    [Fact]
    public void Exception_ThrownAndCaught_PreservesFormattedMessage()
    {
        // Demonstrates the convention end to end: throw, catch, and read the message.
        ClientErrorException? caught = null;
        try
        {
            throw new ClientErrorException(ClientErrorCode.NotFound, 42);
        }
        catch (ClientErrorException exception)
        {
            caught = exception;
        }

        Assert.NotNull(caught);
        Assert.Contains("NotFound", caught!.Message);
        Assert.Contains("42", caught.Message);
    }
}

using HouseholdFinances.Frontend.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace HouseholdFinances.Frontend.Tests.Services;

/// <summary>
/// Tests for the session cookie middleware (issue #28): the cookie is issued on document requests
/// with the hardened flags, an existing session is reused so a reload resumes it, and a cookie
/// whose server-side record is gone is replaced unless a live circuit still holds it. Everything
/// runs against a temporary directory and an ephemeral Data Protection key ring.
/// </summary>
public sealed class SessionCookieMiddlewareTests : IDisposable
{
    private const string ExistingSessionId = "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG";

    private static readonly DateTimeOffset PinnedNow = new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "hf-session-tests", Guid.NewGuid().ToString("N"));
    private readonly IDataProtectionProvider _keyRing = new EphemeralDataProtectionProvider();
    private readonly ActiveSessionRegistry _registry = new();

    private PersistentSessionOptions Options => new(
        _directory,
        Path.Combine(_directory, "keys"),
        TimeSpan.FromDays(PersistentSessionOptions.DefaultCookieMaxAgeDays));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temporary directory is harmless.
        }
    }

    [Fact]
    public async Task InvokeAsync_WithoutASessionCookie_IssuesAHardenedOpaqueCookie()
    {
        var context = CreateDocumentRequest();

        await CreateMiddleware().InvokeAsync(context);

        var setCookie = Assert.Single(context.Response.Headers.SetCookie.ToArray());
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=604800", setCookie, StringComparison.OrdinalIgnoreCase);

        var value = ReadCookieValue(setCookie);
        Assert.Equal(43, value.Length);
        Assert.Equal(value, SessionCookieMiddleware.GetSessionId(context));
    }

    [Fact]
    public async Task InvokeAsync_WithAStoredSession_ReusesTheSameCookieValue()
    {
        var repository = CreateRepository();
        await repository.SetAsync(ExistingSessionId, NewPair());

        var context = CreateDocumentRequest(ExistingSessionId);

        await CreateMiddleware().InvokeAsync(context);

        Assert.Equal(ExistingSessionId, ReadCookieValue(Assert.Single(context.Response.Headers.SetCookie.ToArray())));
        Assert.Equal(ExistingSessionId, SessionCookieMiddleware.GetSessionId(context));
    }

    [Fact]
    public async Task InvokeAsync_WithADanglingCookie_ReplacesItWhenNoCircuitHoldsTheSession()
    {
        var context = CreateDocumentRequest(ExistingSessionId);

        await CreateMiddleware().InvokeAsync(context);

        var value = ReadCookieValue(Assert.Single(context.Response.Headers.SetCookie.ToArray()));
        Assert.NotEqual(ExistingSessionId, value);
        Assert.Equal(value, SessionCookieMiddleware.GetSessionId(context));
    }

    [Fact]
    public async Task InvokeAsync_WithADanglingCookieHeldByALiveCircuit_KeepsItSoSignInIsNotOrphaned()
    {
        _registry.Add(ExistingSessionId);

        var context = CreateDocumentRequest(ExistingSessionId);

        await CreateMiddleware().InvokeAsync(context);

        Assert.Equal(ExistingSessionId, ReadCookieValue(Assert.Single(context.Response.Headers.SetCookie.ToArray())));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("not-a-base64url-value-with-invalid-characters!")]
    public async Task InvokeAsync_WithAMalformedCookie_ReplacesIt(string cookieValue)
    {
        var context = CreateDocumentRequest(cookieValue);

        await CreateMiddleware().InvokeAsync(context);

        var value = ReadCookieValue(Assert.Single(context.Response.Headers.SetCookie.ToArray()));
        Assert.NotEqual(cookieValue, value);
        Assert.Equal(43, value.Length);
    }

    [Fact]
    public async Task InvokeAsync_ForANonDocumentRequest_DoesNotTouchTheCookie()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Headers.Accept = "application/json";
        context.Request.Headers.Cookie = $"{SessionCookieMiddleware.CookieName}={ExistingSessionId}";

        await CreateMiddleware().InvokeAsync(context);

        Assert.Empty(context.Response.Headers.SetCookie.ToArray());
        Assert.Null(SessionCookieMiddleware.GetSessionId(context));
    }

    [Fact]
    public async Task InvokeAsync_ForASignalRRequest_DoesNotTouchTheCookie()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/_blazor/negotiate";
        context.Request.Headers.Accept = "text/html";

        await CreateMiddleware().InvokeAsync(context);

        Assert.Empty(context.Response.Headers.SetCookie.ToArray());
    }

    private SessionCookieMiddleware CreateMiddleware() =>
        new(
            _ => Task.CompletedTask,
            CreateRepository(),
            _registry,
            Options,
            NullLogger<SessionCookieMiddleware>.Instance);

    private FileSessionTokenRepository CreateRepository() =>
        new(Options, _keyRing, new FixedTimeProvider(PinnedNow), NullLogger<FileSessionTokenRepository>.Instance);

    private static DefaultHttpContext CreateDocumentRequest(string? sessionCookie = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/dashboard";
        context.Request.Headers.Accept = "text/html,application/xhtml+xml";

        if (sessionCookie is not null)
        {
            context.Request.Headers.Cookie = $"{SessionCookieMiddleware.CookieName}={sessionCookie}";
        }

        return context;
    }

    private static string ReadCookieValue(string? setCookie)
    {
        Assert.NotNull(setCookie);

        return setCookie.Split(';')[0][(SessionCookieMiddleware.CookieName.Length + 1)..];
    }

    private static TokenPair NewPair() =>
        new("access-1", PinnedNow.AddMinutes(10).UtcDateTime, "refresh-1", PinnedNow.AddDays(1).UtcDateTime);
}

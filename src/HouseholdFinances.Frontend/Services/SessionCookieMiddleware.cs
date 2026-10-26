using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// Issues and reads the opaque session cookie that locates the server-side token record, and hands
/// the identifier to prerendering through <see cref="HttpContext.Items"/>.
/// </summary>
/// <remarks>
/// <para>
/// Only an opaque, random identifier ever reaches the browser. Cookies can only be written on an
/// HTTP response, never from a live Blazor Server circuit, so this middleware runs on document
/// requests: the request that prerenders a page carries the identifier into the circuit, and the
/// same request reissues the cookie with a refreshed lifetime.
/// </para>
/// <para>
/// The cookie is <c>HttpOnly</c>, <c>Secure</c>, <c>SameSite=Lax</c>, host-only, and scoped to
/// <c>/</c>. A cookie whose identifier has no server-side record is a dangling one - left by a
/// sign-out, an expired record, or a restart with a lost key ring - and is replaced here, but only
/// while no live circuit holds that identifier, so a sign-in in progress is never orphaned.
/// </para>
/// </remarks>
public sealed class SessionCookieMiddleware
{
    /// <summary>The name of the session cookie.</summary>
    public const string CookieName = "hf_session";

    // 256 bits of entropy, base64url encoded: 43 characters, well inside the 4 KB cookie limit.
    private const int SessionIdByteLength = 32;
    private const int SessionIdLength = 43;

    private static readonly object SessionIdItemKey = new();

    private readonly RequestDelegate _next;
    private readonly ISessionTokenRepository _repository;
    private readonly ActiveSessionRegistry _activeSessions;
    private readonly PersistentSessionOptions _options;
    private readonly ILogger<SessionCookieMiddleware> _logger;

    /// <summary>Creates the middleware.</summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="repository">The durable session record storage used to detect dangling cookies.</param>
    /// <param name="activeSessions">The registry of identifiers held by live circuits.</param>
    /// <param name="options">The session options supplying the cookie lifetime.</param>
    /// <param name="logger">The logger used to report discarded cookies.</param>
    public SessionCookieMiddleware(
        RequestDelegate next,
        ISessionTokenRepository repository,
        ActiveSessionRegistry activeSessions,
        PersistentSessionOptions options,
        ILogger<SessionCookieMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _activeSessions = activeSessions ?? throw new ArgumentNullException(nameof(activeSessions));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Reads, validates, and reissues the session cookie for a document request.</summary>
    /// <param name="context">The current request.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!IsDocumentRequest(context))
        {
            await _next(context);
            return;
        }

        var presented = context.Request.Cookies[CookieName];
        var sessionId = IsWellFormed(presented) ? presented! : CreateSessionId();

        if (sessionId == presented && !_activeSessions.IsActive(sessionId))
        {
            var stored = await _repository.GetAsync(sessionId, context.RequestAborted);
            if (stored is null)
            {
                // The circuit could not delete the cookie when it cleared the record, so the
                // dangling cookie is replaced here. Nothing is lost: there was no session left.
                _logger.LogDebug("A session cookie without a server-side record was replaced.");
                sessionId = CreateSessionId();
            }
        }

        context.Items[SessionIdItemKey] = sessionId;
        context.Response.Cookies.Append(CookieName, sessionId, CreateCookieOptions());

        await _next(context);
    }

    /// <summary>
    /// Gets the session identifier this request resolved, for the prerendering components that
    /// carry it across the prerender boundary.
    /// </summary>
    /// <param name="httpContext">The current request, or <see langword="null"/> outside a request.</param>
    /// <returns>The resolved identifier, or <see langword="null"/> when the request did not resolve one.</returns>
    public static string? GetSessionId(HttpContext? httpContext) =>
        httpContext?.Items[SessionIdItemKey] as string;

    private CookieOptions CreateCookieOptions() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        MaxAge = _options.CookieMaxAge,
        IsEssential = true
    };

    private static string CreateSessionId() =>
        Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(SessionIdByteLength));

    private static bool IsWellFormed(string? value) =>
        value is not null &&
        value.Length == SessionIdLength &&
        value.All(static character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static bool IsDocumentRequest(HttpContext context)
    {
        // Only a request that renders a document can issue or adopt a session, and only a
        // document request is a navigation that could start a new circuit.
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            return false;
        }

        if (context.Request.Path.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }
}

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// The three states the shell distinguishes when deciding what a user may see: no session, a
/// session without a household, and a session with at least one household.
/// </summary>
public enum AppAccessState
{
    /// <summary>No session is stored, so only the login page is available.</summary>
    SignedOut,

    /// <summary>A session exists but the user belongs to no household.</summary>
    NoHousehold,

    /// <summary>A session exists and the user belongs to at least one household.</summary>
    Ready
}

/// <summary>
/// Decides which navigation entries and pages a user may see, from the existing session and
/// household state. The menu and the shell both read this one answer, so a hidden entry and a
/// blocked page can never disagree.
/// </summary>
/// <remarks>
/// The state comes from <see cref="IAuthenticationService.HasSessionAsync"/> and the shared
/// <see cref="HouseholdContextService"/>, which loads the household list once per circuit. It is
/// scoped, so resolving it once per navigation is enough for the whole shell.
/// </remarks>
public sealed class PageAccessService
{
    /// <summary>The page a signed-out user is sent to, and the only page they may use.</summary>
    public const string LoginPath = "/login";

    /// <summary>The page a signed-in user without a household is sent to.</summary>
    public const string SetupPath = "/setup";

    /// <summary>The page a signed-in member of a household is sent to when they ask for Setup.</summary>
    public const string HouseholdsPath = "/households";

    /// <summary>The profile page, which a user without a household may still use.</summary>
    public const string ProfilePath = "/profile";

    /// <summary>The not-found page, which stays reachable in every state.</summary>
    public const string NotFoundPath = "/not-found";

    /// <summary>The error page, which stays reachable in every state.</summary>
    public const string ErrorPath = "/error";

    // Login is reachable in every state because the page itself redirects a signed-in user to the
    // page that matches their state. Not-found and error stay reachable so a bad URL never traps
    // the user behind the gate.
    private static readonly string[] AlwaysAllowedPaths = { LoginPath, NotFoundPath, ErrorPath };

    private readonly IAuthenticationService _authentication;
    private readonly HouseholdContextService _households;

    /// <summary>Creates the service.</summary>
    /// <param name="authentication">Supplies whether a session is stored.</param>
    /// <param name="households">Supplies the current user's households.</param>
    public PageAccessService(IAuthenticationService authentication, HouseholdContextService households)
    {
        _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        _households = households ?? throw new ArgumentNullException(nameof(households));
    }

    /// <summary>Gets the state resolved by the last <see cref="ResolveAsync"/> call.</summary>
    public AppAccessState State { get; private set; } = AppAccessState.SignedOut;

    /// <summary>
    /// Gets whether the household list could be read on the last <see cref="ResolveAsync"/> call.
    /// A <c>false</c> value means the user is signed in but their household state is unknown.
    /// </summary>
    public bool IsHouseholdStateKnown { get; private set; } = true;

    /// <summary>
    /// Resolves the current state from the stored session and the household list. A list that
    /// cannot be read is reported as <see cref="AppAccessState.Ready"/> with
    /// <see cref="IsHouseholdStateKnown"/> false, so a transient API failure cannot send the user to
    /// Setup and tell them to create a household they already have.
    /// </summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    public async Task ResolveAsync(CancellationToken cancellationToken = default)
    {
        IsHouseholdStateKnown = true;

        if (!await _authentication.HasSessionAsync(cancellationToken))
        {
            State = AppAccessState.SignedOut;
            return;
        }

        try
        {
            await _households.EnsureLoadedAsync(cancellationToken);

            State = _households.Households.Count == 0
                ? AppAccessState.NoHousehold
                : AppAccessState.Ready;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            State = AppAccessState.Ready;
            IsHouseholdStateKnown = false;
        }
    }

    /// <summary>
    /// Returns the page a user in the supplied state belongs on. A redirect always targets this
    /// page, so the check cannot bounce between two pages.
    /// </summary>
    /// <param name="state">The resolved state.</param>
    /// <returns>The API-relative path of that state's page.</returns>
    public static string RedirectTargetFor(AppAccessState state) => state switch
    {
        AppAccessState.NoHousehold => SetupPath,
        AppAccessState.Ready => HouseholdsPath,
        _ => LoginPath
    };

    /// <summary>
    /// Reports whether a page may be rendered in the supplied state.
    /// </summary>
    /// <param name="state">The resolved state.</param>
    /// <param name="relativePath">The page path, with or without a query string or fragment.</param>
    /// <returns><c>true</c> when the page may be shown.</returns>
    public static bool IsAllowed(AppAccessState state, string relativePath)
    {
        string path = Normalize(relativePath);

        if (Array.IndexOf(AlwaysAllowedPaths, path) >= 0)
        {
            return true;
        }

        return state switch
        {
            AppAccessState.SignedOut => false,
            AppAccessState.NoHousehold => string.Equals(path, SetupPath, StringComparison.Ordinal)
                || string.Equals(path, ProfilePath, StringComparison.Ordinal),
            _ => !string.Equals(path, SetupPath, StringComparison.Ordinal)
        };
    }

    /// <summary>
    /// Normalizes a relative page path so comparisons ignore the query string, fragment, trailing
    /// slash and casing. The root becomes <c>/</c>.
    /// </summary>
    /// <param name="relativePath">The page path to normalize.</param>
    /// <returns>The normalized path, always starting with a slash.</returns>
    public static string Normalize(string relativePath)
    {
        string path = relativePath ?? string.Empty;

        int queryStart = path.IndexOfAny(new[] { '?', '#' });
        if (queryStart >= 0)
        {
            path = path[..queryStart];
        }

        path = path.Trim().Trim('/').ToLowerInvariant();

        return path.Length == 0 ? "/" : $"/{path}";
    }
}

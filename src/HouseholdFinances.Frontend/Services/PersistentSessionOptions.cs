using System.Globalization;

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// Where the persisted session lives and how long its cookie may last (issue #28).
/// </summary>
/// <remarks>
/// Everything is configuration-driven so no key material, path, or token is committed. The default
/// locations sit under the current user's local application data folder, which is outside the
/// repository, and the cookie lifetime is bounded so a stale browser cookie expires on its own.
/// </remarks>
public sealed class PersistentSessionOptions
{
    /// <summary>The configuration key holding the session record directory.</summary>
    public const string DirectoryConfigurationKey = "Authentication:Session:Directory";

    /// <summary>The configuration key holding the Data Protection key ring directory.</summary>
    public const string KeyRingDirectoryConfigurationKey = "Authentication:Session:KeyRingDirectory";

    /// <summary>The configuration key holding the session cookie lifetime in days.</summary>
    public const string CookieMaxAgeDaysConfigurationKey = "Authentication:Session:CookieMaxAgeDays";

    /// <summary>The cookie lifetime used when none is configured.</summary>
    public const int DefaultCookieMaxAgeDays = 7;

    /// <summary>The longest cookie lifetime the host accepts.</summary>
    public const int MaximumCookieMaxAgeDays = 90;

    /// <summary>Creates the options from already-resolved values.</summary>
    /// <param name="directory">The absolute directory holding the encrypted session records.</param>
    /// <param name="keyRingDirectory">The absolute directory holding the Data Protection key ring.</param>
    /// <param name="cookieMaxAge">The bounded lifetime of the session cookie.</param>
    public PersistentSessionOptions(string directory, string keyRingDirectory, TimeSpan cookieMaxAge)
    {
        Directory = directory;
        KeyRingDirectory = keyRingDirectory;
        CookieMaxAge = cookieMaxAge;
    }

    /// <summary>Gets the absolute directory holding the encrypted session records.</summary>
    public string Directory { get; }

    /// <summary>Gets the absolute directory holding the Data Protection key ring.</summary>
    public string KeyRingDirectory { get; }

    /// <summary>Gets the bounded lifetime of the session cookie, and of the records it points at.</summary>
    public TimeSpan CookieMaxAge { get; }

    /// <summary>Gets the default session record directory, outside the repository.</summary>
    public static string DefaultDirectory => Path.Combine(DefaultRoot, "sessions");

    /// <summary>Gets the default Data Protection key ring directory, outside the repository.</summary>
    public static string DefaultKeyRingDirectory => Path.Combine(DefaultRoot, "keys");

    /// <summary>
    /// Reads the session options from configuration and validates them at startup, so a
    /// misconfigured path fails immediately instead of silently persisting nothing.
    /// </summary>
    /// <param name="configuration">The host configuration.</param>
    /// <returns>The validated options.</returns>
    /// <exception cref="InvalidOperationException">A configured value is missing or invalid.</exception>
    public static PersistentSessionOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new PersistentSessionOptions(
            ResolveDirectory(configuration[DirectoryConfigurationKey], DefaultDirectory, DirectoryConfigurationKey),
            ResolveDirectory(configuration[KeyRingDirectoryConfigurationKey], DefaultKeyRingDirectory, KeyRingDirectoryConfigurationKey),
            TimeSpan.FromDays(ReadCookieMaxAgeDays(configuration[CookieMaxAgeDaysConfigurationKey])));
    }

    private static string DefaultRoot => Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify),
        "HouseholdFinances",
        "Frontend");

    private static string ResolveDirectory(string? configured, string fallback, string configurationKey)
    {
        var value = string.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim();

        if (!Path.IsPathRooted(value))
        {
            throw new InvalidOperationException(
                $"The '{configurationKey}' configuration value must be an absolute directory path, but was '{value}'.");
        }

        return Path.GetFullPath(value);
    }

    private static int ReadCookieMaxAgeDays(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return DefaultCookieMaxAgeDays;
        }

        if (!int.TryParse(configured, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) ||
            days < 1 ||
            days > MaximumCookieMaxAgeDays)
        {
            throw new InvalidOperationException(
                $"The '{CookieMaxAgeDaysConfigurationKey}' configuration value must be a whole number of days "
                + $"between 1 and {MaximumCookieMaxAgeDays}, but was '{configured}'.");
        }

        return days;
    }
}

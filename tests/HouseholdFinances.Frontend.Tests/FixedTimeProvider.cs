namespace HouseholdFinances.Frontend.Tests;

/// <summary>
/// A deterministic <see cref="TimeProvider"/> double that pins "now" for tests. The production
/// code resolves <see cref="TimeProvider"/> from dependency injection, so no test reads the host
/// clock.
/// </summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    /// <summary>Initializes the provider with the UTC instant the test pins.</summary>
    /// <param name="utcNow">The instant returned by <see cref="GetUtcNow"/>.</param>
    public FixedTimeProvider(DateTimeOffset utcNow) => UtcNow = utcNow;

    /// <summary>Gets or sets the UTC instant the provider reports.</summary>
    public DateTimeOffset UtcNow { get; set; }

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => UtcNow;
}

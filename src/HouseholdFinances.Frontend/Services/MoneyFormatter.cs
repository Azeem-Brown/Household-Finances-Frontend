using System.Globalization;

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// Formats monetary values for display using the money rules decided in issue #3: values stay
/// <see cref="double"/> end-to-end, a single fixed currency is assumed, and amounts are rounded to
/// two decimal places, half away from zero, before being shown.
/// </summary>
public static class MoneyFormatter
{
    /// <summary>The fixed currency symbol shown for every amount (US dollars).</summary>
    public const string CurrencySymbol = "$";

    /// <summary>The number of decimal places shown for every amount.</summary>
    public const int DecimalPlaces = 2;

    // Invariant culture keeps the output identical regardless of the host machine's locale, so the
    // formatted text is deterministic and matches the decided rules.
    private static readonly string AmountFormat = $"#,##0.{new string('0', DecimalPlaces)}";

    /// <summary>
    /// Rounds <paramref name="value"/> to two decimal places, half away from zero, as required by
    /// the money decision.
    /// </summary>
    /// <param name="value">The amount to round.</param>
    /// <returns>The amount rounded to two decimal places.</returns>
    public static double Round(double value) =>
        Math.Round(value, DecimalPlaces, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Formats <paramref name="value"/> as a fixed US dollar amount with a thousands separator and
    /// two decimals, for example <c>$1,234.50</c> or <c>-$40.00</c>.
    /// </summary>
    /// <param name="value">The amount to format.</param>
    /// <returns>The formatted amount, with the sign (when negative) placed before the symbol.</returns>
    public static string Format(double value)
    {
        var rounded = Round(value);
        var magnitude = Math.Abs(rounded).ToString(AmountFormat, CultureInfo.InvariantCulture);

        // Place the sign before the currency symbol ("-$40.00") rather than after ("$-40.00").
        return rounded < 0 ? $"-{CurrencySymbol}{magnitude}" : $"{CurrencySymbol}{magnitude}";
    }
}

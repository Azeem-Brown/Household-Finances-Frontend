using System.Globalization;
using HouseholdFinances.Frontend.Services;

namespace HouseholdFinances.Frontend.Tests.Services;

/// <summary>
/// Tests for the money rules decided in issue #3: a fixed US dollar symbol, two decimal places, and
/// half-away-from-zero rounding.
/// </summary>
public class MoneyFormatterTests
{
    [Theory]
    [InlineData(0, "$0.00")]
    [InlineData(5000, "$5,000.00")]
    [InlineData(1234.5, "$1,234.50")]
    [InlineData(0.001, "$0.00")]
    [InlineData(-40, "-$40.00")]
    public void Format_UsesFixedSymbolTwoDecimalsAndThousandsSeparator(double value, string expected)
    {
        Assert.Equal(expected, MoneyFormatter.Format(value));
    }

    [Theory]
    [InlineData(1.125, "$1.13")]
    [InlineData(0.125, "$0.13")]
    [InlineData(-1.125, "-$1.13")]
    public void Format_RoundsHalfAwayFromZeroNotBankers(double value, string expected)
    {
        // 1.125 and 0.125 are exactly representable, so the rounding rule is observable: half-away
        // -from-zero gives 1.13/0.13, whereas banker's rounding would give 1.12/0.12.
        Assert.Equal(expected, MoneyFormatter.Format(value));
    }

    [Fact]
    public void Format_IsCultureInvariant()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // A comma-decimal culture must not change the rendered amount.
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.Equal("$1,234.50", MoneyFormatter.Format(1234.5));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Round_UsesTwoDecimalPlacesHalfAwayFromZero()
    {
        Assert.Equal(1.13, MoneyFormatter.Round(1.125));
        Assert.Equal(-1.13, MoneyFormatter.Round(-1.125));
    }
}

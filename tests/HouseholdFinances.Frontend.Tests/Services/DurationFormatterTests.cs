using HouseholdFinances.Frontend.Models;
using HouseholdFinances.Frontend.Services;

namespace HouseholdFinances.Frontend.Tests.Services;

/// <summary>
/// Tests for the exact conversion of a number of selected periods into a displayed duration. No
/// approximate weeks-per-month or days-per-month factor is applied.
/// </summary>
public class DurationFormatterTests
{
    [Theory]
    [InlineData(Interval.Daily, 1, "1 day")]
    [InlineData(Interval.Daily, 2, "2 days")]
    [InlineData(Interval.Weekly, 1, "1 week")]
    [InlineData(Interval.Weekly, 3, "3 weeks")]
    [InlineData(Interval.BiWeekly, 1, "2 weeks")]
    [InlineData(Interval.BiWeekly, 2, "4 weeks")]
    [InlineData(Interval.Monthly, 1, "1 month")]
    [InlineData(Interval.Monthly, 10, "10 months")]
    [InlineData(Interval.Quarterly, 1, "3 months")]
    [InlineData(Interval.Quarterly, 2, "6 months")]
    [InlineData(Interval.Yearly, 1, "12 months")]
    [InlineData(Interval.Yearly, 2, "24 months")]
    public void Format_ConvertsExactlyAndUsesSingularForOneUnit(
        Interval period,
        int periods,
        string expected)
    {
        Assert.Equal(expected, DurationFormatter.Format(period, periods));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Format_NonPositivePeriodCount_Throws(int periods)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DurationFormatter.Format(Interval.Monthly, periods));
    }

    [Fact]
    public void Format_UnsupportedInterval_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DurationFormatter.Format((Interval)99, 1));
    }
}

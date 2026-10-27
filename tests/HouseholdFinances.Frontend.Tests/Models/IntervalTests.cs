using HouseholdFinances.Frontend.Models;

namespace HouseholdFinances.Frontend.Tests.Models;

/// <summary>
/// Verifies the <see cref="Interval"/> enum matches the recorded Interval decision (#4).
/// </summary>
public class IntervalTests
{
    [Fact]
    public void Interval_HasExactlyTheDecidedMembersInOrder()
    {
        var expected = new[]
        {
            "Daily",
            "Weekly",
            "BiWeekly",
            "Monthly",
            "Quarterly",
            "Yearly"
        };

        var actual = Enum.GetNames<Interval>();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Interval_MembersHaveStableExplicitValues()
    {
        Assert.Equal(0, (int)Interval.Daily);
        Assert.Equal(1, (int)Interval.Weekly);
        Assert.Equal(2, (int)Interval.BiWeekly);
        Assert.Equal(3, (int)Interval.Monthly);
        Assert.Equal(4, (int)Interval.Quarterly);
        Assert.Equal(5, (int)Interval.Yearly);
    }
}

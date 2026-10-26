using HouseholdFinances.Frontend.Models;
using HouseholdFinances.Frontend.Services;

namespace HouseholdFinances.Frontend.Tests.Services;

/// <summary>
/// Tests for the goal projection: the shortfall is the target minus the current position, a reached
/// goal produces no time to goal, and a zero or negative contribution is never divided into.
/// </summary>
public class GoalProjectionCalculatorTests
{
    [Fact]
    public void Project_ShortfallIsTargetMinusCurrentPosition()
    {
        GoalProjectionResult projection = GoalProjectionCalculator.Project(GoalWith(10000.0), 2500.0, 0.0);

        Assert.Equal(7500.0, projection.Shortfall);
        Assert.False(projection.IsReached);
        Assert.False(projection.HasTimeToGoal);
    }

    [Fact]
    public void Project_ExactlyAtTarget_IsReachedWithNoPeriods()
    {
        GoalProjectionResult projection = GoalProjectionCalculator.Project(GoalWith(1000.0), 1000.0, 500.0);

        Assert.Equal(0.0, projection.Shortfall);
        Assert.True(projection.IsReached);
        Assert.False(projection.HasTimeToGoal);
        Assert.Null(projection.PeriodsNeeded);
    }

    [Fact]
    public void Project_AboveTarget_ReportsOverTargetAndNoPeriods()
    {
        GoalProjectionResult projection = GoalProjectionCalculator.Project(GoalWith(1000.0), 1200.0, 500.0);

        Assert.Equal(-200.0, projection.Shortfall);
        Assert.True(projection.IsReached);
        Assert.Null(projection.PeriodsNeeded);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-100.0)]
    public void Project_ZeroOrNegativeContribution_ShowsNoPeriods(double contribution)
    {
        GoalProjectionResult projection = GoalProjectionCalculator.Project(GoalWith(1000.0), 0.0, contribution);

        Assert.Equal(1000.0, projection.Shortfall);
        Assert.False(projection.IsReached);
        Assert.False(projection.HasTimeToGoal);
        Assert.Null(projection.PeriodsNeeded);
    }

    [Theory]
    [InlineData(7500.0, 1000.0, 8)]
    [InlineData(8000.0, 1000.0, 8)]
    [InlineData(1.0, 1000.0, 1)]
    public void Project_PositiveContribution_RoundsShortfallUpToWholePeriods(
        double target,
        double contribution,
        int expectedPeriods)
    {
        GoalProjectionResult projection = GoalProjectionCalculator.Project(GoalWith(target), 0.0, contribution);

        Assert.True(projection.HasTimeToGoal);
        Assert.Equal(expectedPeriods, projection.PeriodsNeeded);
    }

    [Fact]
    public void Project_SubCentContribution_RoundsToZeroAndDoesNotDivide()
    {
        GoalProjectionResult projection = GoalProjectionCalculator.Project(GoalWith(1000.0), 0.0, 0.004);

        Assert.False(projection.HasTimeToGoal);
        Assert.Null(projection.PeriodsNeeded);
    }

    [Fact]
    public void Project_RoundsMoneyBeforeProjecting()
    {
        // A sub-cent current position rounds to zero, so it cannot create a phantom period.
        GoalProjectionResult projection = GoalProjectionCalculator.Project(GoalWith(100.0), 0.004, 100.0);

        Assert.Equal(100.0, projection.Shortfall);
        Assert.Equal(1, projection.PeriodsNeeded);
    }

    [Fact]
    public void Project_NullGoal_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => GoalProjectionCalculator.Project(null!, 0.0, 100.0));
    }

    private static Goal GoalWith(double value) =>
        new()
        {
            Name = "Holiday",
            Value = value
        };
}

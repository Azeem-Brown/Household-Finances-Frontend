using HouseholdFinances.Frontend.Models;

namespace HouseholdFinances.Frontend.Services;

/// <summary>
/// The outcome of comparing a goal's target with the household's current financial position.
/// </summary>
/// <param name="Shortfall">
/// The target minus the current position, rounded to the decided money precision. A zero or
/// negative value means the goal has been reached.
/// </param>
/// <param name="PeriodsNeeded">
/// The whole number of periodic contributions still needed, or <see langword="null"/> when the goal
/// is already reached or no positive contribution is available.
/// </param>
public readonly record struct GoalProjectionResult(double Shortfall, int? PeriodsNeeded)
{
    /// <summary>Whether the current position meets or exceeds the target.</summary>
    public bool IsReached => Shortfall <= 0;

    /// <summary>Whether a positive contribution makes a time-to-goal projection possible.</summary>
    public bool HasTimeToGoal => PeriodsNeeded is not null;
}

/// <summary>
/// Projects how long a goal takes to reach from the household's current financial position.
/// <para>
/// The goal's <see cref="Goal.Value"/> is its target and <see cref="Goal.Total"/> is the amount
/// contributed so far, so the shortfall is <c>Value - current position</c>; this follows the
/// projection recorded on issue #49 and never multiplies the target by occurrence counts. The
/// contribution is a constant amount per selected period, and the projection rounds the shortfall
/// up to whole contributions because a partial contribution cannot finish the goal.
/// </para>
/// </summary>
public static class GoalProjectionCalculator
{
    /// <summary>
    /// Projects the shortfall and the number of contributions needed to reach <paramref name="goal"/>.
    /// </summary>
    /// <param name="goal">The goal whose target is projected.</param>
    /// <param name="currentPosition">The household's current position toward the goal.</param>
    /// <param name="contribution">
    /// The constant amount available per period, for example the accepted savings allocation.
    /// </param>
    /// <returns>
    /// The rounded shortfall and, when the goal is still ahead and the contribution is positive, the
    /// number of whole contributions needed. A zero or negative contribution yields no projection
    /// rather than dividing by zero.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="goal"/> is null.</exception>
    public static GoalProjectionResult Project(Goal goal, double currentPosition, double contribution)
    {
        ArgumentNullException.ThrowIfNull(goal);

        // Round at the money boundary first, so a sub-cent residue cannot create a phantom period.
        double shortfall = MoneyFormatter.Round(goal.Value) - MoneyFormatter.Round(currentPosition);
        double roundedContribution = MoneyFormatter.Round(contribution);

        if (shortfall <= 0 || roundedContribution <= 0)
        {
            return new GoalProjectionResult(shortfall, null);
        }

        int periodsNeeded = (int)Math.Ceiling(shortfall / roundedContribution);

        return new GoalProjectionResult(shortfall, periodsNeeded);
    }
}

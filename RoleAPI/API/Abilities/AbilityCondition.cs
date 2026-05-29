using System;
using LabApi.Features.Wrappers;

namespace RoleAPI.API.Abilities;

public abstract class AbilityCondition
{
    /// <summary>
    ///     Gets the message displayed to the player when this condition is not met.
    /// </summary>
    public abstract string FailureMessage { get; }

    /// <summary>
    ///     Evaluates whether this condition is currently satisfied for the given player.
    /// </summary>
    /// <param name="player">The player to check the condition for.</param>
    /// <returns><see langword="true" /> if the condition is met; otherwise <see langword="false" />.</returns>
    public abstract bool IsMet(Player player);

    /// <summary>
    ///     Creates an inline condition from a predicate and a failure message.
    /// </summary>
    /// <param name="predicate">Returns <see langword="true" /> when the condition is satisfied.</param>
    /// <param name="failureMessage">Message shown to the player when the predicate returns <see langword="false" />.</param>
    public static AbilityCondition Create(Func<Player, bool> predicate, string failureMessage)
    {
        return new LambdaCondition(predicate, failureMessage);
    }

    private sealed class LambdaCondition : AbilityCondition
    {
        private readonly Func<Player, bool> _predicate;

        internal LambdaCondition(Func<Player, bool> predicate, string failureMessage)
        {
            _predicate = predicate;
            FailureMessage = failureMessage;
        }

        public override string FailureMessage { get; }

        public override bool IsMet(Player player)
        {
            return _predicate(player);
        }
    }
}
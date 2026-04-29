using SteamWatch.Core.Limits;

namespace SteamWatch.Core.Enforcement;

public sealed class ForceClosePolicy
{
    public static readonly TimeSpan DefaultCountdown = TimeSpan.FromSeconds(60);

    private readonly TimeSpan _countdown;

    public ForceClosePolicy(TimeSpan? countdown = null)
    {
        _countdown = countdown ?? DefaultCountdown;
    }

    public ForceCloseDecision Evaluate(
        LimitEvaluation evaluation,
        DateTimeOffset now,
        DateTimeOffset? existingDeadline = null,
        bool gameStillRunning = true)
    {
        if (!evaluation.IsExceeded || evaluation.Rule.Enforcement != EnforcementMode.ForceClose)
        {
            return new ForceCloseDecision(ForceCloseCountdownState.NotRequired, null, evaluation.Rule);
        }

        if (!gameStillRunning)
        {
            return new ForceCloseDecision(ForceCloseCountdownState.Cancelled, null, evaluation.Rule);
        }

        if (existingDeadline is null)
        {
            return new ForceCloseDecision(ForceCloseCountdownState.Waiting, now.Add(_countdown), evaluation.Rule);
        }

        if (now >= existingDeadline.Value)
        {
            return new ForceCloseDecision(ForceCloseCountdownState.ReadyToClose, existingDeadline, evaluation.Rule);
        }

        return new ForceCloseDecision(ForceCloseCountdownState.Waiting, existingDeadline, evaluation.Rule);
    }
}

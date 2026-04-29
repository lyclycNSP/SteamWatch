using SteamWatch.Core.Limits;

namespace SteamWatch.Core.Enforcement;

public sealed record ForceCloseDecision(
    ForceCloseCountdownState State,
    DateTimeOffset? Deadline,
    LimitRule Rule)
{
    public bool ShouldStartCountdown => State == ForceCloseCountdownState.Waiting && Deadline is not null;

    public bool ShouldCloseNow => State == ForceCloseCountdownState.ReadyToClose;
}

namespace SteamWatch.Core.Limits;

public sealed record LimitRule(
    LimitScope Scope,
    LimitPeriod Period,
    int MaxMinutes,
    EnforcementMode Enforcement,
    int? AppId = null,
    string Name = "")
{
    public bool IsEnabled => MaxMinutes > 0;
}

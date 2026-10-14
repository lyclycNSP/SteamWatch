namespace SteamWatch.Core.Limits;

public sealed record LimitRule(
    LimitScope Scope,
    LimitPeriod Period,
    int MaxMinutes,
    EnforcementMode Enforcement,
    int? AppId = null,
    string Name = "")
{
    public string Identity => $"{Scope}:{Period}:{(Scope == LimitScope.Global ? 0 : AppId)}";

    public LimitRule Normalize()
    {
        if (!Enum.IsDefined(Scope) || !Enum.IsDefined(Period) || !Enum.IsDefined(Enforcement)
            || MaxMinutes < 0 || (Scope == LimitScope.Game && AppId is not > 0))
            throw new ArgumentException("限额规则包含无效范围、周期、策略或游戏。");
        return Scope == LimitScope.Global ? this with { AppId = null } : this;
    }

    public bool IsEnabled => MaxMinutes > 0;
}

using SteamWatch.App.Presentation;
using SteamWatch.Core.Limits;

namespace SteamWatch.App;

public sealed record RuleTarget(int AppId, string Name);

public sealed class LimitRuleEditorViewModel
{
    private readonly Func<IReadOnlyList<LimitRule>> _getRules;
    private readonly bool _forceClose;
    public bool IsGlobal { get; }
    public LimitRule? Original { get; private set; }
    public RuleTarget? Target { get; private set; }
    public LimitPeriod Period { get; private set; }
    public int Minutes => Original?.MaxMinutes ?? 60;
    public EnforcementMode Enforcement => _forceClose ? EnforcementMode.ForceClose
        : Original?.Enforcement ?? EnforcementMode.NotifyOnly;

    public LimitRuleEditorViewModel(Func<IReadOnlyList<LimitRule>> getRules, LimitRule? original,
        RuleTarget? target = null, LimitPeriod? globalPeriod = null, bool forceClose = false)
    {
        _getRules = getRules;
        _forceClose = forceClose;
        IsGlobal = globalPeriod is not null || original?.Scope == LimitScope.Global;
        Period = globalPeriod ?? original?.Period ?? LimitPeriod.Day;
        Original = original;
        Target = target ?? (original?.AppId is { } id ? new RuleTarget(id, original.Name) : null);
        if (!IsGlobal) SelectGame(Target, Period);
    }

    public void SelectGame(RuleTarget? target, LimitPeriod period)
    {
        if (IsGlobal) throw new InvalidOperationException("总限额的目标和周期不可切换。");
        Target = target;
        Period = period;
        Original = _getRules().FirstOrDefault(rule => rule.Scope == LimitScope.Game
            && rule.AppId == target?.AppId && rule.Period == period);
    }

    public LimitRule CreateCandidate(double minutes, EnforcementMode enforcement)
    {
        if (!double.IsFinite(minutes) || minutes < 1 || minutes > 10080 || minutes != Math.Truncate(minutes))
            throw new ArgumentException("请输入 1–10080 之间的整数分钟。");
        if (!IsGlobal && Target is null) throw new ArgumentException("请选择目标游戏。");
        var candidate = new LimitRule(IsGlobal ? LimitScope.Global : LimitScope.Game, Period, (int)minutes,
            enforcement, IsGlobal ? null : Target!.AppId, IsGlobal ? "全部游戏" : Target!.Name).Normalize();
        if (HasConflict(candidate)) throw new InvalidOperationException("该目标和周期已有规则，请重新打开该游戏的限额设置。");
        return candidate;
    }

    public bool HasConflict(LimitRule candidate) =>
        RuntimePresentation.HasIdentityConflict(_getRules(), candidate, Original);
}

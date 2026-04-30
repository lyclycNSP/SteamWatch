using SteamWatch.Core.Limits;

namespace SteamWatch.App;

public sealed class LimitRuleRowViewModel
{
    public LimitRuleRowViewModel()
    {
    }

    public LimitRuleRowViewModel(LimitRule rule)
    {
        Rule = rule;
        ScopeText = rule.Scope == LimitScope.Global ? "全部游戏" : rule.Name;
        PeriodText = rule.Period == LimitPeriod.Day ? "每日" : "每周";
        LimitText = $"{rule.MaxMinutes} 分钟";
        EnforcementText = rule.Enforcement == EnforcementMode.ForceClose ? "强制退出" : "仅提醒";
    }

    public LimitRule? Rule { get; set; }

    public string ScopeText { get; set; } = string.Empty;

    public string PeriodText { get; set; } = string.Empty;

    public string LimitText { get; set; } = string.Empty;

    public string EnforcementText { get; set; } = string.Empty;

    public string SummaryText => $"{ScopeText} / {PeriodText} / {LimitText}";
}

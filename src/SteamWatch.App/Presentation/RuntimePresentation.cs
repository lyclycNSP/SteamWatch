using SteamWatch.Core.Limits;
using SteamWatch.Core.Tracking;
using SteamWatch.Infrastructure.Runtime;

namespace SteamWatch.App.Presentation;

/// <summary>Projects the combined historical records without changing the runtime's statistics contract.</summary>
public static class RuntimePresentation
{
    public static string GameRuleSummary(IEnumerable<LimitRule> rules)
    {
        var lines = rules.Where(rule => rule.Scope == LimitScope.Game).OrderBy(rule => rule.Period)
            .Select(rule => $"{(rule.Period == LimitPeriod.Day ? "每日" : "每周")} {rule.MaxMinutes} 分钟 · "
                + (!rule.IsEnabled ? "已停用" : rule.Enforcement == EnforcementMode.ForceClose ? "超限强退" : "仅提醒"))
            .ToArray();
        return lines.Length == 0 ? "未设置" : string.Join("\n", lines);
    }

    public static IReadOnlyList<DailyPlaytimeRecord> MeasuredRecords(RuntimeSnapshot snapshot)
    {
        var estimates = snapshot.EstimatedRecords.ToDictionary(day => day.Date);
        return snapshot.Records.Select(day => new DailyPlaytimeRecord(day.Date,
            day.GameMinutes.ToDictionary(pair => pair.Key, pair => Math.Max(0,
                pair.Value - (estimates.GetValueOrDefault(day.Date)?.GetGameMinutes(pair.Key) ?? 0))))).ToList();
    }

    public static BudgetPresentation Budget(int measuredMinutes, LimitRule? rule, bool paused)
    {
        var enabled = rule is { IsEnabled: true };
        var usage = enabled ? $"{measuredMinutes} / {rule!.MaxMinutes} 分钟" : $"{measuredMinutes} 分钟 · 未设置";
        var policy = rule?.Enforcement == EnforcementMode.ForceClose ? "强制退出" : "仅提醒";
        var remaining = enabled
            ? measuredMinutes > rule!.MaxMinutes ? $"已超出 {measuredMinutes - rule.MaxMinutes} 分钟" : $"剩余 {rule.MaxMinutes - measuredMinutes} 分钟"
            : "设置全部游戏共同使用的时长上限";
        return new(usage, enabled ? $"{remaining} · {(paused ? "限制已暂停" : policy)}" : remaining,
            enabled ? Math.Clamp(100d * measuredMinutes / rule!.MaxMinutes, 0, 100) : 0, enabled);
    }

    public static bool HasIdentityConflict(IEnumerable<LimitRule> rules, LimitRule candidate, LimitRule? original) =>
        rules.Any(rule => rule.Identity == candidate.Identity && rule.Identity != original?.Identity);

    public static IReadOnlyList<DayStatisticsPresentation> WeekStatistics(RuntimeSnapshot snapshot, DateOnly date)
    {
        var start = WeekCalculator.GetWeekStart(date);
        var measured = MeasuredRecords(snapshot).ToDictionary(day => day.Date, day => day.TotalMinutes);
        var estimated = snapshot.EstimatedRecords.ToDictionary(day => day.Date, day => day.TotalMinutes);
        return Enumerable.Range(0, 7).Select(offset => start.AddDays(offset))
            .Select(day => new DayStatisticsPresentation(day, measured.GetValueOrDefault(day), estimated.GetValueOrDefault(day))).ToList();
    }
}

public sealed record BudgetPresentation(string Usage, string Detail, double Progress, bool IsConfigured);

public sealed record DayStatisticsPresentation(DateOnly Date, int MeasuredMinutes, int EstimatedMinutes)
{
    public int TotalMinutes => MeasuredMinutes + EstimatedMinutes;
    public string DateText => Date.ToString("MM-dd");
    public string AccessibleSummary => $"{Date:yyyy-MM-dd}：实测 {MeasuredMinutes} 分钟，估算 {EstimatedMinutes} 分钟，合计 {TotalMinutes} 分钟";
}

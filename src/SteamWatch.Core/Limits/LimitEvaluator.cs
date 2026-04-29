using SteamWatch.Core.Limits;
using SteamWatch.Core.Tracking;

namespace SteamWatch.Core.Limits;

public sealed record LimitEvaluation(
    LimitRule Rule,
    int UsedMinutes,
    double Progress,
    bool IsExceeded)
{
    public int RemainingMinutes => Math.Max(0, Rule.MaxMinutes - UsedMinutes);
}

public sealed class LimitEvaluator
{
    public LimitEvaluation Evaluate(LimitRule rule, IReadOnlyCollection<DailyPlaytimeRecord> records, DateOnly today)
    {
        if (!rule.IsEnabled)
        {
            return new LimitEvaluation(rule, 0, 0, false);
        }

        var usedMinutes = rule.Period switch
        {
            LimitPeriod.Day => GetDayMinutes(rule, records, today),
            LimitPeriod.Week => GetWeekMinutes(rule, records, today),
            _ => throw new ArgumentOutOfRangeException(nameof(rule.Period), rule.Period, "Unsupported limit period.")
        };

        var progress = (double)usedMinutes / rule.MaxMinutes;
        return new LimitEvaluation(rule, usedMinutes, progress, usedMinutes >= rule.MaxMinutes);
    }

    private static int GetDayMinutes(LimitRule rule, IEnumerable<DailyPlaytimeRecord> records, DateOnly today)
    {
        var record = records.FirstOrDefault(item => item.Date == today);
        return record is null ? 0 : GetMinutesForScope(rule, record);
    }

    private static int GetWeekMinutes(LimitRule rule, IEnumerable<DailyPlaytimeRecord> records, DateOnly today)
    {
        var start = WeekCalculator.GetWeekStart(today);
        var end = WeekCalculator.GetWeekEnd(today);

        return records
            .Where(item => item.Date >= start && item.Date <= end)
            .Sum(item => GetMinutesForScope(rule, item));
    }

    private static int GetMinutesForScope(LimitRule rule, DailyPlaytimeRecord record)
    {
        return rule.Scope switch
        {
            LimitScope.Game => rule.AppId is null ? 0 : record.GetGameMinutes(rule.AppId.Value),
            LimitScope.Global => record.TotalMinutes,
            _ => throw new ArgumentOutOfRangeException(nameof(rule.Scope), rule.Scope, "Unsupported limit scope.")
        };
    }
}

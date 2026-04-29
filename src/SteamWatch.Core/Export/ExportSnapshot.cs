using SteamWatch.Core.Tracking;

namespace SteamWatch.Core.Export;

public sealed record ExportSnapshot(
    DateTimeOffset ExportedAt,
    IReadOnlyList<ExportGame> Games,
    IReadOnlyList<ExportPlaytimeRecord> PlaytimeRecords,
    IReadOnlyList<ExportLimitRule> LimitRules,
    ExportSettings Settings)
{
    public IReadOnlyList<WeeklyPlaytimeSummary> GetWeeklySummaries()
    {
        return PlaytimeRecords
            .GroupBy(record => new
            {
                WeekStart = WeekCalculator.GetWeekStart(record.Date),
                record.AppId,
                record.GameName
            })
            .Select(group => new WeeklyPlaytimeSummary(
                group.Key.WeekStart,
                WeekCalculator.GetWeekEnd(group.Key.WeekStart),
                group.Key.AppId,
                group.Key.GameName,
                group.Sum(item => item.Minutes)))
            .OrderBy(item => item.WeekStart)
            .ThenBy(item => item.GameName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}

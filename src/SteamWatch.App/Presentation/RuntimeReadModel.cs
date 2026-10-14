using SteamWatch.Core.Limits;
using SteamWatch.Core.Tracking;
using SteamWatch.Infrastructure.Runtime;

namespace SteamWatch.App.Presentation;

/// <summary>One published runtime generation and one local date for a complete UI update.</summary>
public sealed class RuntimeReadModel(RuntimeSnapshot source, DateOnly today, string? error = null)
{
    public RuntimeSnapshot Source { get; } = source;
    public DateOnly Today { get; } = today;
    public string StatusText { get; } = error ?? source.Status;
    public PlaytimeRecordBook MeasuredBook { get; } = new(RuntimePresentation.MeasuredRecords(source));

    public (BudgetPresentation Day, BudgetPresentation Week) Budgets =>
        (RuntimePresentation.Budget(MeasuredBook.GetTotalMinutes(Today), Rule(LimitPeriod.Day), Source.IsPaused),
         RuntimePresentation.Budget(MeasuredBook.GetWeekTotalMinutes(Today), Rule(LimitPeriod.Week), Source.IsPaused));

    private LimitRule? Rule(LimitPeriod period) => Source.Rules.FirstOrDefault(rule =>
        rule.Scope == LimitScope.Global && rule.Period == period);

    public IReadOnlyList<StatisticsRow> DailyStats(DateOnly date) => Stats(date, date);
    public IReadOnlyList<StatisticsRow> WeeklyStats(DateOnly date) =>
        Stats(WeekCalculator.GetWeekStart(date), WeekCalculator.GetWeekEnd(date));
    public IReadOnlyList<DayStatisticsPresentation> WeekStatistics(DateOnly date)
    {
        var start = WeekCalculator.GetWeekStart(date);
        var end = WeekCalculator.GetWeekEnd(date);
        var estimated = Source.EstimatedRecords.Where(day => day.Date >= start && day.Date <= end)
            .ToDictionary(day => day.Date, day => day.TotalMinutes);
        return Enumerable.Range(0, 7).Select(offset => start.AddDays(offset))
            .Select(day => new DayStatisticsPresentation(day, MeasuredBook.GetTotalMinutes(day),
                estimated.GetValueOrDefault(day))).ToList();
    }

    private IReadOnlyList<StatisticsRow> Stats(DateOnly start, DateOnly end)
    {
        var estimates = Source.EstimatedRecords.Where(day => day.Date >= start && day.Date <= end)
            .SelectMany(day => day.GameMinutes).GroupBy(pair => pair.Key)
            .ToDictionary(group => group.Key, group => group.Sum(pair => pair.Value));
        var icons = Source.Games.ToDictionary(game => game.Info.AppId, game => game.Info.IconPath);
        return Source.Records.Where(day => day.Date >= start && day.Date <= end)
            .SelectMany(day => day.GameMinutes).GroupBy(pair => pair.Key)
            .Select(group => new StatisticsRow(group.Key,
                Source.GameNames.GetValueOrDefault(group.Key) ?? $"AppID {group.Key}",
                group.Sum(pair => pair.Value), estimates.GetValueOrDefault(group.Key), icons.GetValueOrDefault(group.Key)))
            .Where(row => row.TotalMinutes > 0).OrderByDescending(row => row.TotalMinutes).ToList();
    }
}

public sealed record StatisticsRow(int AppId, string Name, int TotalMinutes, int EstimatedMinutes, string? IconPath)
{
    public int MeasuredMinutes => Math.Max(0, TotalMinutes - EstimatedMinutes);
}

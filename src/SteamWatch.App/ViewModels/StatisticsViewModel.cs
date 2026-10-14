using SteamWatch.Core.Tracking;

namespace SteamWatch.App;

public sealed record StatisticsTotals(int MeasuredMinutes, int EstimatedMinutes, int GameCount)
{
    public int TotalMinutes => MeasuredMinutes + EstimatedMinutes;
    public bool IsEmpty => GameCount == 0;
    public string Breakdown => $"实测 {MeasuredMinutes} 分钟 · 估算 {EstimatedMinutes} 分钟 · 估算不参与限额";
}

/// <summary>Statistics selection and summaries, independent of WinUI and runtime ownership.</summary>
public sealed class StatisticsViewModel(DateOnly today)
{
    public DateOnly DailyDate { get; private set; } = today;
    public DateOnly WeeklyDate { get; private set; } = today;
    public DateOnly WeekStart => WeekCalculator.GetWeekStart(WeeklyDate);
    public DateOnly WeekEnd => WeekCalculator.GetWeekEnd(WeekStart);
    public bool IsWeekly { get; private set; }
    public StatisticsTotals DailyTotals { get; private set; } = new(0, 0, 0);
    public StatisticsTotals WeeklyTotals { get; private set; } = new(0, 0, 0);
    public string Subtitle => IsWeekly ? "周一至周日 · 实测、估算与合计" : "按日期查看 · 实测、估算与合计";
    public string DailySummary => $"{DailyDate:yyyy-MM-dd} 总时长 {DailyTotals.TotalMinutes} 分钟";
    public string WeeklySummary => $"{WeekStart:yyyy-MM-dd} - {WeekEnd:yyyy-MM-dd} 总时长 {WeeklyTotals.TotalMinutes} 分钟";

    public void SelectPeriod(bool weekly) => IsWeekly = weekly;
    public void SelectDailyDate(DateOnly date) => DailyDate = date;
    public void SelectWeeklyDate(DateOnly date) => WeeklyDate = date;
    public void UpdateTotals(StatisticsTotals daily, StatisticsTotals weekly)
    {
        DailyTotals = daily;
        WeeklyTotals = weekly;
    }
}

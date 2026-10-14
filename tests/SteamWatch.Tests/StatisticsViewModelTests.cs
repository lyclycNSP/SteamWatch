using SteamWatch.App;

namespace SteamWatch.Tests;

[TestClass]
public sealed class StatisticsViewModelTests
{
    [TestMethod]
    public void DayWeekAndPeriodSelectionsRemainIndependentAfterRefresh()
    {
        var state = new StatisticsViewModel(new(2026, 10, 12));
        state.SelectDailyDate(new(2026, 9, 30));
        state.SelectWeeklyDate(new(2026, 10, 4));
        state.SelectPeriod(true);
        state.UpdateTotals(new(10, 5, 1), new(20, 8, 2));
        state.SelectPeriod(false);
        state.UpdateTotals(new(12, 5, 1), new(22, 8, 2));
        Assert.AreEqual(new DateOnly(2026, 9, 30), state.DailyDate);
        Assert.AreEqual(new DateOnly(2026, 10, 4), state.WeeklyDate);
        Assert.AreEqual(new DateOnly(2026, 9, 28), state.WeekStart);
        Assert.IsFalse(state.IsWeekly);
        Assert.AreEqual("2026-09-30 总时长 17 分钟", state.DailySummary);
    }

    [TestMethod]
    public void WeekRangeIncludesSundayAndCrossesYearBoundary()
    {
        var state = new StatisticsViewModel(new(2027, 1, 3));
        Assert.AreEqual(new DateOnly(2026, 12, 28), state.WeekStart);
        Assert.AreEqual(new DateOnly(2027, 1, 3), state.WeekEnd);
        Assert.AreEqual("2026-12-28 - 2027-01-03 总时长 0 分钟", state.WeeklySummary);
    }

    [TestMethod]
    public void CorrectionChangesEstimateSummaryWithoutChangingMeasuredValue()
    {
        var state = new StatisticsViewModel(new(2026, 10, 12));
        state.UpdateTotals(new(1, 15, 1), new(10, 15, 2));
        state.UpdateTotals(new(1, 5, 1), new(10, 5, 2));
        Assert.AreEqual(1, state.DailyTotals.MeasuredMinutes);
        Assert.AreEqual(10, state.WeeklyTotals.MeasuredMinutes);
        Assert.AreEqual("2026-10-12 总时长 6 分钟", state.DailySummary);
        Assert.AreEqual("实测 1 分钟 · 估算 5 分钟 · 估算不参与限额", state.DailyTotals.Breakdown);
        Assert.IsFalse(state.DailyTotals.IsEmpty);
    }

    [TestMethod]
    public void ZeroMinuteRecordIsDifferentFromNoRecords()
    {
        var state = new StatisticsViewModel(new(2026, 10, 12));
        Assert.IsTrue(state.DailyTotals.IsEmpty);
        state.UpdateTotals(new(0, 0, 1), new(0, 0, 0));
        Assert.IsFalse(state.DailyTotals.IsEmpty);
        Assert.IsTrue(state.WeeklyTotals.IsEmpty);
    }

    [TestMethod]
    public void SubtitleFollowsTheSelectedPeriodAcrossDataUpdates()
    {
        var state = new StatisticsViewModel(new(2026, 10, 12));
        Assert.AreEqual("按日期查看 · 实测、估算与合计", state.Subtitle);
        state.SelectPeriod(true);
        state.UpdateTotals(new(1, 0, 1), new(2, 0, 1));
        Assert.AreEqual("周一至周日 · 实测、估算与合计", state.Subtitle);
        Assert.IsTrue(state.IsWeekly);
    }
}

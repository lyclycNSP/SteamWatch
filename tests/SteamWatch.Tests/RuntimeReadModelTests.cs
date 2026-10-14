using SteamWatch.App.Presentation;
using SteamWatch.Core.Limits;
using SteamWatch.Infrastructure.Runtime;

namespace SteamWatch.Tests;

[TestClass]
public sealed class RuntimeReadModelTests
{
    [TestMethod]
    public void ChartAcrossYearBoundaryMatchesMeasuredBudgetAndKeepsEstimateOnlyDays()
    {
        var today = new DateOnly(2027, 1, 1);
        var monday = new DateOnly(2026, 12, 28);
        var snapshot = RuntimeSnapshot.Empty with
        {
            Records = [new(monday.AddDays(-1), new Dictionary<int, int> { [730] = 999 }),
                new(today, new Dictionary<int, int> { [730] = 4, [570] = 6 }),
                new(monday.AddDays(6), new Dictionary<int, int> { [730] = 9 }),
                new(monday.AddDays(7), new Dictionary<int, int> { [730] = 777 })],
            EstimatedRecords = [new(today, new Dictionary<int, int> { [730] = 10 }),
                new(monday.AddDays(6), new Dictionary<int, int> { [730] = 9 })]
        };
        var model = new RuntimeReadModel(snapshot, today);
        var days = model.WeekStatistics(today);
        Assert.HasCount(7, days);
        Assert.AreEqual(monday, days[0].Date);
        Assert.AreEqual(monday.AddDays(6), days[6].Date);
        Assert.AreEqual(6, days.Sum(day => day.MeasuredMinutes)); // Clamp per game before summing.
        Assert.AreEqual(19, days.Sum(day => day.EstimatedMinutes));
        Assert.AreEqual(model.MeasuredBook.GetWeekTotalMinutes(today), days.Sum(day => day.MeasuredMinutes));
        CollectionAssert.AreEqual(RuntimePresentation.WeekStatistics(snapshot, today).ToArray(), days.ToArray());
        Assert.AreEqual(9, days[6].EstimatedMinutes);
        Assert.AreEqual(0, days[6].MeasuredMinutes);
    }

    private static readonly DateOnly Monday = new(2026, 10, 12);
    private static RuntimeSnapshot Fixture() => RuntimeSnapshot.Empty with
    {
        Records = [new(Monday, new Dictionary<int, int> { [730] = 25, [42] = 5 }),
            new(Monday.AddDays(-1), new Dictionary<int, int> { [730] = 90 })],
        EstimatedRecords = [new(Monday, new Dictionary<int, int> { [730] = 15 })],
        GameNames = new Dictionary<int, string> { [730] = "Fixture" },
        Rules = [new(LimitScope.Global, LimitPeriod.Day, 60, EnforcementMode.NotifyOnly)]
    };

    [TestMethod]
    public void BudgetRowsAndChartUseSameMeasuredAndEstimatedValues()
    {
        var model = new RuntimeReadModel(Fixture(), Monday);
        Assert.AreEqual(15, model.MeasuredBook.GetTotalMinutes(Monday));
        StringAssert.Contains(model.Budgets.Day.Usage, "15 / 60");
        Assert.AreEqual(30, model.DailyStats(Monday).Sum(row => row.TotalMinutes));
        Assert.AreEqual(15, model.DailyStats(Monday).Sum(row => row.EstimatedMinutes));
        Assert.AreEqual(15, model.WeekStatistics(Monday)[0].MeasuredMinutes);
        Assert.AreEqual(15, model.WeekStatistics(Monday)[0].EstimatedMinutes);
    }

    [TestMethod]
    public void CapturedGenerationDoesNotFollowNewPublication()
    {
        var published = Fixture();
        var captured = new RuntimeReadModel(published, Monday);
        published = published with { Records = [], Rules = [], IsPaused = true };
        var newer = new RuntimeReadModel(published, Monday.AddDays(1));
        Assert.AreEqual(30, captured.DailyStats(Monday).Sum(row => row.TotalMinutes));
        Assert.IsTrue(captured.Budgets.Day.IsConfigured);
        Assert.AreEqual(Monday, captured.Today);
        Assert.IsEmpty(newer.DailyStats(Monday));
        Assert.IsFalse(newer.Budgets.Day.IsConfigured);
    }

    [TestMethod]
    public void SundayUsesMondayRangeAndIncludesHistoricalNames()
    {
        var rows = new RuntimeReadModel(Fixture(), Monday).WeeklyStats(Monday.AddDays(6));
        Assert.AreEqual(30, rows.Sum(row => row.TotalMinutes));
        Assert.AreEqual("Fixture", rows[0].Name);
        Assert.AreEqual("AppID 42", rows[1].Name);
        Assert.IsNull(rows[0].IconPath);
    }

    [TestMethod]
    public void EmptyDateHasSevenZeroChartDaysAndNoInventedRows()
    {
        var model = new RuntimeReadModel(Fixture(), Monday);
        Assert.IsEmpty(model.DailyStats(Monday.AddDays(8)));
        var days = model.WeekStatistics(Monday.AddDays(8));
        Assert.HasCount(7, days);
        Assert.AreEqual(0, days.Sum(day => day.TotalMinutes));
    }

    [TestMethod]
    public void CapturedErrorAndPauseStatusDoNotAlterTimeValues()
    {
        var model = new RuntimeReadModel(Fixture() with { IsPaused = true }, Monday, "monitor error");
        Assert.AreEqual("monitor error", model.StatusText);
        StringAssert.Contains(model.Budgets.Day.Detail, "限制已暂停");
        Assert.AreEqual(15, model.MeasuredBook.GetTotalMinutes(Monday));
    }
}

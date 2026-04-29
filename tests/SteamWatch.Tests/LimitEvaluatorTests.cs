using SteamWatch.Core.Limits;
using SteamWatch.Core.Tracking;

namespace SteamWatch.Tests;

[TestClass]
public sealed class LimitEvaluatorTests
{
    [TestMethod]
    public void Evaluate_GameDailyLimit_UsesOnlyTodayGameMinutes()
    {
        var today = new DateOnly(2026, 4, 29);
        var records = new[]
        {
            new DailyPlaytimeRecord(today, new Dictionary<int, int> { [730] = 90, [570] = 30 }),
            new DailyPlaytimeRecord(today.AddDays(-1), new Dictionary<int, int> { [730] = 200 })
        };
        var rule = new LimitRule(LimitScope.Game, LimitPeriod.Day, 120, EnforcementMode.NotifyOnly, 730);

        var result = new LimitEvaluator().Evaluate(rule, records, today);

        Assert.AreEqual(90, result.UsedMinutes);
        Assert.AreEqual(0.75, result.Progress, 0.001);
        Assert.IsFalse(result.IsExceeded);
    }

    [TestMethod]
    public void Evaluate_GameWeeklyLimit_UsesMondayThroughSunday()
    {
        var today = new DateOnly(2026, 4, 29);
        var records = new[]
        {
            new DailyPlaytimeRecord(new DateOnly(2026, 4, 27), new Dictionary<int, int> { [730] = 60 }),
            new DailyPlaytimeRecord(new DateOnly(2026, 4, 29), new Dictionary<int, int> { [730] = 90 }),
            new DailyPlaytimeRecord(new DateOnly(2026, 4, 26), new Dictionary<int, int> { [730] = 999 })
        };
        var rule = new LimitRule(LimitScope.Game, LimitPeriod.Week, 120, EnforcementMode.ForceClose, 730);

        var result = new LimitEvaluator().Evaluate(rule, records, today);

        Assert.AreEqual(150, result.UsedMinutes);
        Assert.IsTrue(result.IsExceeded);
    }

    [TestMethod]
    public void Evaluate_GlobalDailyLimit_SumsAllGamesForToday()
    {
        var today = new DateOnly(2026, 4, 29);
        var records = new[]
        {
            new DailyPlaytimeRecord(today, new Dictionary<int, int> { [730] = 90, [570] = 45 })
        };
        var rule = new LimitRule(LimitScope.Global, LimitPeriod.Day, 120, EnforcementMode.NotifyOnly);

        var result = new LimitEvaluator().Evaluate(rule, records, today);

        Assert.AreEqual(135, result.UsedMinutes);
        Assert.IsTrue(result.IsExceeded);
    }

    [TestMethod]
    public void GetWeekStart_ReturnsMonday()
    {
        var date = new DateOnly(2026, 4, 29);

        var weekStart = WeekCalculator.GetWeekStart(date);

        Assert.AreEqual(new DateOnly(2026, 4, 27), weekStart);
    }
}

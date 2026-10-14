using SteamWatch.App.Presentation;
using SteamWatch.Core.Limits;
using SteamWatch.Core.Tracking;
using SteamWatch.Infrastructure.Runtime;

namespace SteamWatch.Tests;

[TestClass]
public sealed class RuntimePresentationTests
{
    [TestMethod]
    [DataRow(LimitPeriod.Day, EnforcementMode.NotifyOnly, "每日 60 分钟 · 仅提醒")]
    [DataRow(LimitPeriod.Day, EnforcementMode.ForceClose, "每日 60 分钟 · 超限强退")]
    [DataRow(LimitPeriod.Week, EnforcementMode.NotifyOnly, "每周 60 分钟 · 仅提醒")]
    [DataRow(LimitPeriod.Week, EnforcementMode.ForceClose, "每周 60 分钟 · 超限强退")]
    public void GameRuleSummaryAssociatesPolicyWithItsOwnPeriod(LimitPeriod period, EnforcementMode mode, string expected)
        => Assert.AreEqual(expected, RuntimePresentation.GameRuleSummary([new(LimitScope.Game, period, 60, mode, 730)]));

    [TestMethod]
    public void MixedPoliciesRemainSeparateAndDailyPrecedesWeekly()
    {
        var week = new LimitRule(LimitScope.Game, LimitPeriod.Week, 300, EnforcementMode.ForceClose, 730);
        var day = week with { Period = LimitPeriod.Day, MaxMinutes = 60, Enforcement = EnforcementMode.NotifyOnly };
        Assert.AreEqual("每日 60 分钟 · 仅提醒\n每周 300 分钟 · 超限强退", RuntimePresentation.GameRuleSummary([week, day]));
    }

    [TestMethod]
    public void DisabledStoredRuleDoesNotAppearToEnforceForceClose()
        => Assert.AreEqual("每日 0 分钟 · 已停用", RuntimePresentation.GameRuleSummary(
            [new(LimitScope.Game, LimitPeriod.Day, 0, EnforcementMode.ForceClose, 730)]));

    [TestMethod]
    public void GlobalBudgetDoesNotInventSingleGameRules()
    {
        Assert.AreEqual("未设置", RuntimePresentation.GameRuleSummary([]));
        Assert.AreEqual("未设置", RuntimePresentation.GameRuleSummary([new(LimitScope.Global, LimitPeriod.Day, 60, EnforcementMode.ForceClose)]));
    }

    [TestMethod]
    public void MeasuredProjectionSubtractsEstimatesPerGameAndPreservesStatistics()
    {
        var monday = new DateOnly(2026, 10, 12);
        var snapshot = RuntimeSnapshot.Empty with
        {
            Records = [new(monday, new Dictionary<int, int> { [730] = 16, [42] = 9 }),
                new(monday.AddDays(-1), new Dictionary<int, int> { [730] = 30 })],
            EstimatedRecords = [new(monday, new Dictionary<int, int> { [730] = 15 })]
        };
        var book = new PlaytimeRecordBook(RuntimePresentation.MeasuredRecords(snapshot));
        Assert.AreEqual(1, book.GetGameMinutes(monday, 730));
        Assert.AreEqual(10, book.GetTotalMinutes(monday));
        Assert.AreEqual(10, book.GetWeekTotalMinutes(monday));
        Assert.AreEqual(25, snapshot.Records[0].TotalMinutes);
        Assert.AreEqual(15, snapshot.EstimatedRecords[0].TotalMinutes);
    }

    [TestMethod]
    public void UnconfiguredBudgetDoesNotInventRemainingTimeOrProgress()
    {
        var budget = RuntimePresentation.Budget(45, null, false);
        Assert.IsFalse(budget.IsConfigured);
        Assert.AreEqual(0d, budget.Progress);
        StringAssert.Contains(budget.Usage, "45 分钟 · 未设置");
        Assert.DoesNotContain("剩余", budget.Detail);
    }

    [TestMethod]
    public void GlobalBudgetShowsMeasuredRemainingAndClampsExceededProgress()
    {
        var rule = new LimitRule(LimitScope.Global, LimitPeriod.Day, 120, EnforcementMode.NotifyOnly);
        var budget = RuntimePresentation.Budget(45, rule, false);
        Assert.AreEqual(37.5, budget.Progress);
        StringAssert.Contains(budget.Detail, "剩余 75 分钟 · 仅提醒");
        var exceeded = RuntimePresentation.Budget(140, rule, true);
        Assert.AreEqual(100d, exceeded.Progress);
        StringAssert.Contains(exceeded.Detail, "已超出 20 分钟 · 限制已暂停");
    }

    [TestMethod]
    public void RuleIdentityChangesCannotOverwriteAnotherPeriod()
    {
        var day = new LimitRule(LimitScope.Game, LimitPeriod.Day, 60, EnforcementMode.NotifyOnly, 730);
        var week = day with { Period = LimitPeriod.Week, MaxMinutes = 600 };
        Assert.IsTrue(RuntimePresentation.HasIdentityConflict([day, week], day with { Period = LimitPeriod.Week }, day));
        Assert.IsFalse(RuntimePresentation.HasIdentityConflict([day, week], day with { MaxMinutes = 90 }, day));
        Assert.IsTrue(RuntimePresentation.HasIdentityConflict([day], day, null));
        Assert.IsFalse(RuntimePresentation.HasIdentityConflict([day], week, null));
    }

    [TestMethod]
    public void WeeklyTrendUsesMondayBoundariesAndSeparatesMeasuredFromEstimated()
    {
        var monday = new DateOnly(2026, 10, 12);
        var snapshot = RuntimeSnapshot.Empty with
        {
            Records = [new(monday.AddDays(-1), new Dictionary<int, int> { [730] = 999 }),
                new(monday, new Dictionary<int, int> { [730] = 16, [42] = 4 }),
                new(monday.AddDays(6), new Dictionary<int, int> { [42] = 9 }),
                new(monday.AddDays(7), new Dictionary<int, int> { [42] = 777 })],
            EstimatedRecords = [new(monday, new Dictionary<int, int> { [730] = 15 }),
                new(monday.AddDays(6), new Dictionary<int, int> { [42] = 2 })]
        };
        var days = RuntimePresentation.WeekStatistics(snapshot, monday.AddDays(3));
        Assert.HasCount(7, days);
        Assert.AreEqual(monday, days[0].Date);
        Assert.AreEqual(monday.AddDays(6), days[6].Date);
        Assert.AreEqual(12, days.Sum(day => day.MeasuredMinutes));
        Assert.AreEqual(17, days.Sum(day => day.EstimatedMinutes));
        Assert.AreEqual(29, days.Sum(day => day.TotalMinutes));
        Assert.AreEqual(0, days[1].TotalMinutes);
        Assert.AreEqual(15, snapshot.EstimatedRecords[0].TotalMinutes);
    }

    [TestMethod]
    public void EmptyWeekHasSevenZeroDaysWithoutInventingHistory()
    {
        var snapshot = RuntimeSnapshot.Empty;
        var days = RuntimePresentation.WeekStatistics(snapshot, new DateOnly(2026, 10, 18));
        Assert.HasCount(7, days);
        Assert.IsTrue(days.All(day => day.TotalMinutes == 0));
        Assert.IsEmpty(snapshot.Records);
        StringAssert.Contains(days[0].AccessibleSummary, "实测 0 分钟，估算 0 分钟，合计 0 分钟");
    }
}

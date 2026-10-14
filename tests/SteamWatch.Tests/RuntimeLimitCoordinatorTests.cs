using SteamWatch.Core.Enforcement;
using SteamWatch.Core.Limits;
using SteamWatch.Core.Reminders;
using SteamWatch.Core.Tracking;

namespace SteamWatch.Tests;

[TestClass]
public sealed class RuntimeLimitCoordinatorTests
{
    [TestMethod]
    public void Evaluate_MixedRulesAcrossYearBoundary_MatchesIndependentEvaluation()
    {
        var today = new DateOnly(2027, 1, 1);
        var records = Enumerable.Range(-400, 405).Select(offset =>
            new DailyPlaytimeRecord(today.AddDays(offset), new Dictionary<int, int> { [730] = 2, [570] = 3 }))
            .Reverse().ToList();
        // The evaluator uses the first matching daily record and all weekly records.
        records.Insert(0, new(today, new Dictionary<int, int> { [730] = 7 }));
        var rules = new[] { LimitScope.Game, LimitScope.Global }.SelectMany(scope =>
            new[] { LimitPeriod.Day, LimitPeriod.Week }.Select(period =>
                new LimitRule(scope, period, 100, EnforcementMode.NotifyOnly, scope == LimitScope.Game ? 730 : null)))
            .ToArray();
        var result = new RuntimeLimitCoordinator().Evaluate(rules, records, today, new HashSet<int> { 730 },
            new DateTimeOffset(2027, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var evaluator = new LimitEvaluator();
        for (var i = 0; i < rules.Length; i++)
            Assert.AreEqual(evaluator.Evaluate(rules[i].Normalize(), records, today), result.Decisions[i].Evaluation);
    }

    [TestMethod]
    public void Evaluate_DailyRuleAtMaximumDate_DoesNotRequireRepresentableWeekEnd()
    {
        var result = new RuntimeLimitCoordinator().Evaluate(
            [new(LimitScope.Global, LimitPeriod.Day, 100, EnforcementMode.NotifyOnly)],
            [new(DateOnly.MaxValue, new Dictionary<int, int> { [730] = 5 })],
            DateOnly.MaxValue, new HashSet<int>(), DateTimeOffset.MaxValue);
        Assert.AreEqual(5, result.Decisions[0].Evaluation.UsedMinutes);
    }

    [TestMethod]
    public void Evaluate_RunningGameCrossesThreshold_CreatesReminder()
    {
        var coordinator = new RuntimeLimitCoordinator();
        var today = new DateOnly(2026, 4, 30);
        var now = new DateTimeOffset(2026, 4, 30, 20, 0, 0, TimeSpan.Zero);
        var rules = new[]
        {
            new LimitRule(LimitScope.Game, LimitPeriod.Day, 100, EnforcementMode.NotifyOnly, 730, "CS2")
        };
        var records = new[]
        {
            new DailyPlaytimeRecord(today, new Dictionary<int, int> { [730] = 70 })
        };

        var result = coordinator.Evaluate(rules, records, today, new HashSet<int> { 730 }, now);

        Assert.IsTrue(result.HasReminder);
        Assert.AreEqual(ReminderLevel.FirstWarning, result.Decisions[0].Reminder.Level);
    }

    [TestMethod]
    public void Evaluate_SameReminderBeforeInterval_DoesNotRepeat()
    {
        var coordinator = new RuntimeLimitCoordinator();
        var today = new DateOnly(2026, 4, 30);
        var now = new DateTimeOffset(2026, 4, 30, 20, 0, 0, TimeSpan.Zero);
        var rules = new[]
        {
            new LimitRule(LimitScope.Game, LimitPeriod.Day, 100, EnforcementMode.NotifyOnly, 730, "CS2")
        };
        var records = new[]
        {
            new DailyPlaytimeRecord(today, new Dictionary<int, int> { [730] = 70 })
        };

        coordinator.Evaluate(rules, records, today, new HashSet<int> { 730 }, now);
        var result = coordinator.Evaluate(rules, records, today, new HashSet<int> { 730 }, now.AddMinutes(1));

        Assert.IsFalse(result.HasReminder);
    }

    [TestMethod]
    public void Evaluate_ForceCloseExceeded_StartsCountdownThenReadyToClose()
    {
        var coordinator = new RuntimeLimitCoordinator(forceClosePolicy: new ForceClosePolicy(TimeSpan.FromSeconds(30)));
        var today = new DateOnly(2026, 4, 30);
        var now = new DateTimeOffset(2026, 4, 30, 20, 0, 0, TimeSpan.Zero);
        var rules = new[]
        {
            new LimitRule(LimitScope.Game, LimitPeriod.Day, 60, EnforcementMode.ForceClose, 730, "CS2")
        };
        var records = new[]
        {
            new DailyPlaytimeRecord(today, new Dictionary<int, int> { [730] = 61 })
        };

        var first = coordinator.Evaluate(rules, records, today, new HashSet<int> { 730 }, now);
        var second = coordinator.Evaluate(rules, records, today, new HashSet<int> { 730 }, now.AddSeconds(30));

        Assert.IsTrue(first.HasForceCloseCountdown);
        Assert.AreEqual(ForceCloseCountdownState.Waiting, first.Decisions[0].ForceClose.State);
        Assert.IsTrue(second.HasReadyToClose);
        Assert.AreEqual(ForceCloseCountdownState.ReadyToClose, second.Decisions[0].ForceClose.State);
    }

    [TestMethod]
    public void Evaluate_GameNotRunning_DoesNotNotifyOrForceClose()
    {
        var coordinator = new RuntimeLimitCoordinator();
        var today = new DateOnly(2026, 4, 30);
        var rules = new[]
        {
            new LimitRule(LimitScope.Game, LimitPeriod.Day, 60, EnforcementMode.ForceClose, 730, "CS2")
        };
        var records = new[]
        {
            new DailyPlaytimeRecord(today, new Dictionary<int, int> { [730] = 61 })
        };

        var result = coordinator.Evaluate(
            rules,
            records,
            today,
            new HashSet<int>(),
            new DateTimeOffset(2026, 4, 30, 20, 0, 0, TimeSpan.Zero));

        Assert.IsFalse(result.HasReminder);
        Assert.IsFalse(result.HasForceCloseCountdown);
        Assert.AreEqual(ForceCloseCountdownState.Cancelled, result.Decisions[0].ForceClose.State);
    }
}

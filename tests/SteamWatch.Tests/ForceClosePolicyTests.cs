using SteamWatch.Core.Enforcement;
using SteamWatch.Core.Limits;

namespace SteamWatch.Tests;

[TestClass]
public sealed class ForceClosePolicyTests
{
    [TestMethod]
    public void Evaluate_NotifyOnlyExceeded_DoesNotRequireForceClose()
    {
        var rule = new LimitRule(LimitScope.Game, LimitPeriod.Day, 60, EnforcementMode.NotifyOnly, 730);
        var evaluation = new LimitEvaluation(rule, 61, 1.01, true);

        var decision = new ForceClosePolicy().Evaluate(evaluation, DateTimeOffset.UtcNow);

        Assert.AreEqual(ForceCloseCountdownState.NotRequired, decision.State);
        Assert.IsFalse(decision.ShouldCloseNow);
    }

    [TestMethod]
    public void Evaluate_ForceCloseExceeded_StartsCountdown()
    {
        var now = new DateTimeOffset(2026, 4, 29, 20, 0, 0, TimeSpan.Zero);
        var rule = new LimitRule(LimitScope.Game, LimitPeriod.Day, 60, EnforcementMode.ForceClose, 730);
        var evaluation = new LimitEvaluation(rule, 61, 1.01, true);

        var decision = new ForceClosePolicy(TimeSpan.FromSeconds(30)).Evaluate(evaluation, now);

        Assert.AreEqual(ForceCloseCountdownState.Waiting, decision.State);
        Assert.AreEqual(now.AddSeconds(30), decision.Deadline);
        Assert.IsTrue(decision.ShouldStartCountdown);
    }

    [TestMethod]
    public void Evaluate_DeadlineReached_IsReadyToClose()
    {
        var now = new DateTimeOffset(2026, 4, 29, 20, 0, 30, TimeSpan.Zero);
        var deadline = new DateTimeOffset(2026, 4, 29, 20, 0, 30, TimeSpan.Zero);
        var rule = new LimitRule(LimitScope.Game, LimitPeriod.Day, 60, EnforcementMode.ForceClose, 730);
        var evaluation = new LimitEvaluation(rule, 61, 1.01, true);

        var decision = new ForceClosePolicy().Evaluate(evaluation, now, deadline);

        Assert.AreEqual(ForceCloseCountdownState.ReadyToClose, decision.State);
        Assert.IsTrue(decision.ShouldCloseNow);
    }

    [TestMethod]
    public void Evaluate_GameAlreadyStopped_CancelsCountdown()
    {
        var rule = new LimitRule(LimitScope.Game, LimitPeriod.Day, 60, EnforcementMode.ForceClose, 730);
        var evaluation = new LimitEvaluation(rule, 61, 1.01, true);

        var decision = new ForceClosePolicy().Evaluate(
            evaluation,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddSeconds(30),
            gameStillRunning: false);

        Assert.AreEqual(ForceCloseCountdownState.Cancelled, decision.State);
        Assert.IsFalse(decision.ShouldCloseNow);
    }
}

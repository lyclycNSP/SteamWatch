using SteamWatch.Core.Reminders;

namespace SteamWatch.Tests;

[TestClass]
public sealed class ReminderPolicyTests
{
    [TestMethod]
    [DataRow(0.69, ReminderLevel.None)]
    [DataRow(0.70, ReminderLevel.FirstWarning)]
    [DataRow(0.85, ReminderLevel.SecondWarning)]
    [DataRow(0.95, ReminderLevel.FinalWarning)]
    [DataRow(1.00, ReminderLevel.Exceeded)]
    public void CalculateLevel_UsesEscalationThresholds(double progress, ReminderLevel expected)
    {
        var policy = new ReminderPolicy();

        var level = policy.CalculateLevel(progress);

        Assert.AreEqual(expected, level);
    }

    [TestMethod]
    public void CalculateLevel_UsesCustomThresholds()
    {
        var policy = new ReminderPolicy(new ReminderThresholdSettings(50, 75, 90));

        Assert.AreEqual(ReminderLevel.None, policy.CalculateLevel(0.49));
        Assert.AreEqual(ReminderLevel.FirstWarning, policy.CalculateLevel(0.50));
        Assert.AreEqual(ReminderLevel.SecondWarning, policy.CalculateLevel(0.75));
        Assert.AreEqual(ReminderLevel.FinalWarning, policy.CalculateLevel(0.90));
    }

    [TestMethod]
    public void Evaluate_LevelIncrease_NotifiesImmediately()
    {
        var policy = new ReminderPolicy();
        var now = new DateTimeOffset(2026, 4, 29, 20, 0, 0, TimeSpan.Zero);
        var state = new ReminderState(1, ReminderLevel.FirstWarning, now.AddMinutes(-1), 1);

        var decision = policy.Evaluate(state, 0.86, now);

        Assert.IsTrue(decision.ShouldNotify);
        Assert.AreEqual(ReminderLevel.SecondWarning, decision.Level);
        Assert.AreEqual(2, decision.NextState.ReminderCount);
    }

    [TestMethod]
    public void Evaluate_SameLevelBeforeInterval_DoesNotNotify()
    {
        var policy = new ReminderPolicy();
        var now = new DateTimeOffset(2026, 4, 29, 20, 0, 0, TimeSpan.Zero);
        var state = new ReminderState(1, ReminderLevel.Exceeded, now.AddSeconds(-30), 1);

        var decision = policy.Evaluate(state, 1.1, now);

        Assert.IsFalse(decision.ShouldNotify);
        Assert.AreEqual(1, decision.NextState.ReminderCount);
    }

    [TestMethod]
    public void Evaluate_SameLevelAfterInterval_NotifiesAgain()
    {
        var policy = new ReminderPolicy();
        var now = new DateTimeOffset(2026, 4, 29, 20, 0, 0, TimeSpan.Zero);
        var state = new ReminderState(1, ReminderLevel.Exceeded, now.AddMinutes(-2), 1);

        var decision = policy.Evaluate(state, 1.1, now);

        Assert.IsTrue(decision.ShouldNotify);
        Assert.AreEqual(2, decision.NextState.ReminderCount);
    }
}

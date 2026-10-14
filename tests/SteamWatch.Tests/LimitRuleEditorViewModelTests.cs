using SteamWatch.App;
using SteamWatch.Core.Limits;

namespace SteamWatch.Tests;

[TestClass]
public sealed class LimitRuleEditorViewModelTests
{
    private static readonly RuleTarget Game = new(730, "Fixture");
    private static LimitRule Rule(LimitPeriod period, int minutes, EnforcementMode policy = EnforcementMode.NotifyOnly) =>
        new(LimitScope.Game, period, minutes, policy, Game.AppId, Game.Name);

    [TestMethod]
    public void SwitchingPeriodsLoadsTheMatchingExistingRule()
    {
        var day = Rule(LimitPeriod.Day, 60);
        var week = Rule(LimitPeriod.Week, 300, EnforcementMode.ForceClose);
        var state = new LimitRuleEditorViewModel(() => [day, week], null, Game);
        Assert.AreSame(day, state.Original);
        state.SelectGame(Game, LimitPeriod.Week);
        Assert.AreSame(week, state.Original);
        Assert.AreEqual(300, state.Minutes);
        Assert.AreEqual(EnforcementMode.ForceClose, state.Enforcement);
        Assert.AreEqual(350, state.CreateCandidate(350, state.Enforcement).MaxMinutes);
        state.SelectGame(Game, LimitPeriod.Day);
        Assert.AreEqual(60, state.Minutes);
    }

    [TestMethod]
    public void NewTargetAndMissingPeriodUseDefaultsInsteadOfAnotherRule()
    {
        var state = new LimitRuleEditorViewModel(() => [Rule(LimitPeriod.Day, 90)], null, Game);
        state.SelectGame(Game, LimitPeriod.Week);
        Assert.IsNull(state.Original);
        Assert.AreEqual(60, state.Minutes);
        state.SelectGame(new(731, "Other"), LimitPeriod.Day);
        Assert.AreEqual(731, state.CreateCandidate(120, state.Enforcement).AppId);
        Assert.IsNull(state.Original);
    }

    [TestMethod]
    public void ForcePolicyActionOverridesBothPeriodsWithoutChangingStoredRules()
    {
        var day = Rule(LimitPeriod.Day, 60);
        var week = Rule(LimitPeriod.Week, 300);
        var state = new LimitRuleEditorViewModel(() => [day, week], null, Game, forceClose: true);
        Assert.AreEqual(EnforcementMode.ForceClose, state.Enforcement);
        state.SelectGame(Game, LimitPeriod.Week);
        Assert.AreEqual(EnforcementMode.ForceClose, state.Enforcement);
        Assert.AreEqual(EnforcementMode.NotifyOnly, week.Enforcement);
    }

    [TestMethod]
    public void GlobalTargetAndPeriodStayPinned()
    {
        var original = new LimitRule(LimitScope.Global, LimitPeriod.Week, 600, EnforcementMode.NotifyOnly);
        var state = new LimitRuleEditorViewModel(() => [original], original, globalPeriod: LimitPeriod.Week);
        var candidate = state.CreateCandidate(700, EnforcementMode.ForceClose);
        Assert.AreEqual(LimitScope.Global, candidate.Scope);
        Assert.AreEqual(LimitPeriod.Week, candidate.Period);
        Assert.IsNull(candidate.AppId);
        Assert.ThrowsExactly<InvalidOperationException>(() => state.SelectGame(Game, LimitPeriod.Day));
    }

    [TestMethod]
    public void InvalidMinutesAreRejectedWithoutChangingOriginal()
    {
        var original = Rule(LimitPeriod.Day, 60);
        var state = new LimitRuleEditorViewModel(() => [original], original);
        foreach (var minutes in new[] { double.NaN, double.PositiveInfinity, -1, 0, 1.5, 10081 })
            Assert.ThrowsExactly<ArgumentException>(() => state.CreateCandidate(minutes, EnforcementMode.NotifyOnly));
        Assert.AreSame(original, state.Original);
        Assert.AreEqual(1, state.CreateCandidate(1, EnforcementMode.NotifyOnly).MaxMinutes);
        Assert.AreEqual(10080, state.CreateCandidate(10080, EnforcementMode.NotifyOnly).MaxMinutes);
    }

    [TestMethod]
    public void CandidateCapturesItsTargetBeforeLaterSelectionChanges()
    {
        var state = new LimitRuleEditorViewModel(() => [], null, Game);
        var candidate = state.CreateCandidate(60, EnforcementMode.NotifyOnly);
        state.SelectGame(new(731, "Other"), LimitPeriod.Week);
        Assert.AreEqual(730, candidate.AppId);
        Assert.AreEqual(LimitPeriod.Day, candidate.Period);
        Assert.AreEqual("Fixture", candidate.Name);
    }

    [TestMethod]
    public void ConflictsAreRecheckedAgainstTheLatestRules()
    {
        var rules = new List<LimitRule>();
        var state = new LimitRuleEditorViewModel(() => rules, null, Game);
        var candidate = state.CreateCandidate(60, EnforcementMode.NotifyOnly);
        rules.Add(Rule(LimitPeriod.Day, 90));
        Assert.IsTrue(state.HasConflict(candidate));
        Assert.ThrowsExactly<InvalidOperationException>(() => state.CreateCandidate(60, EnforcementMode.NotifyOnly));
    }

    [TestMethod]
    public void MissingTargetIsRejected()
    {
        var state = new LimitRuleEditorViewModel(() => [], null);
        Assert.ThrowsExactly<ArgumentException>(() => state.CreateCandidate(60, EnforcementMode.NotifyOnly));
    }
}

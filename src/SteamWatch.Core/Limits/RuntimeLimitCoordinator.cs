using SteamWatch.Core.Enforcement;
using SteamWatch.Core.Reminders;
using SteamWatch.Core.Tracking;

namespace SteamWatch.Core.Limits;

public sealed class RuntimeLimitCoordinator
{
    private readonly LimitEvaluator _limitEvaluator;
    private ReminderPolicy _reminderPolicy;
    private ForceClosePolicy _forceClosePolicy;
    private readonly Dictionary<int, ReminderState> _reminderStates = [];
    private readonly Dictionary<int, DateTimeOffset> _forceCloseDeadlines = [];

    private readonly Dictionary<string, int> _identities = [];
    private readonly Dictionary<int, (LimitRule Rule, DateOnly PeriodStart)> _versions = [];
    private int _nextRuleId;

    public void Configure(ReminderPolicy reminderPolicy, ForceClosePolicy forceClosePolicy)
    {
        _reminderPolicy = reminderPolicy;
        _forceClosePolicy = forceClosePolicy;
    }

    public void Reset()
    {
        _reminderStates.Clear();
        _forceCloseDeadlines.Clear();
        _versions.Clear();
    }

    public RuntimeLimitCoordinator(
        LimitEvaluator? limitEvaluator = null,
        ReminderPolicy? reminderPolicy = null,
        ForceClosePolicy? forceClosePolicy = null)
    {
        _limitEvaluator = limitEvaluator ?? new LimitEvaluator();
        _reminderPolicy = reminderPolicy ?? new ReminderPolicy();
        _forceClosePolicy = forceClosePolicy ?? new ForceClosePolicy();
    }

    public RuntimeLimitCheckResult Evaluate(
        IReadOnlyList<LimitRule> rules,
        IReadOnlyCollection<DailyPlaytimeRecord> records,
        DateOnly today,
        IReadOnlySet<int> runningAppIds,
        DateTimeOffset now)
    {
        var decisions = new List<RuntimeLimitDecision>();

        // Both supported periods lie within this week. Filter once for all rules,
        // retaining record order and duplicate dates for the evaluator's existing semantics.
        var weekStart = WeekCalculator.GetWeekStart(today);
        var weekEndDay = weekStart.DayNumber + 6;
        var periodRecords = records.Where(record => record.Date >= weekStart && record.Date.DayNumber <= weekEndDay).ToArray();

        var liveIds = rules.Select(rule => rule.Identity).ToHashSet();
        foreach (var removed in _identities.Keys.Where(key => !liveIds.Contains(key)).ToList())
        {
            var removedId = _identities[removed];
            _reminderStates.Remove(removedId);
            _forceCloseDeadlines.Remove(removedId);
            _versions.Remove(removedId);
            _identities.Remove(removed);
        }
        foreach (var input in rules)
        {
            var rule = input.Normalize();
            if (!rule.IsEnabled)
            {
                if (_identities.TryGetValue(rule.Identity, out var disabledId))
                {
                    _reminderStates.Remove(disabledId);
                    _forceCloseDeadlines.Remove(disabledId);
                    _versions.Remove(disabledId);
                }
                continue;
            }

            if (!_identities.TryGetValue(rule.Identity, out var ruleId))
                _identities[rule.Identity] = ruleId = ++_nextRuleId;
            var version = (rule, rule.Period == LimitPeriod.Day ? today : WeekCalculator.GetWeekStart(today));
            if (!_versions.TryGetValue(ruleId, out var previous) || previous != version)
            {
                _reminderStates.Remove(ruleId);
                _forceCloseDeadlines.Remove(ruleId);
                _versions[ruleId] = version;
            }
            var isRunning = IsRuleTargetRunning(rule, runningAppIds);
            var evaluation = _limitEvaluator.Evaluate(rule, periodRecords, today);

            var reminderDecision = EvaluateReminder(ruleId, evaluation, isRunning, now);
            var forceCloseDecision = EvaluateForceClose(ruleId, evaluation, isRunning, now);

            decisions.Add(new RuntimeLimitDecision(ruleId, evaluation, reminderDecision, forceCloseDecision));
        }

        return new RuntimeLimitCheckResult(decisions);
    }

    private ReminderDecision EvaluateReminder(
        int ruleId,
        LimitEvaluation evaluation,
        bool isRunning,
        DateTimeOffset now)
    {
        var state = _reminderStates.GetValueOrDefault(ruleId, new ReminderState(ruleId));
        if (!isRunning)
        {
            var nextState = state with { CurrentLevel = ReminderLevel.None };
            _reminderStates[ruleId] = nextState;
            return ReminderDecision.NoReminder(ReminderLevel.None, nextState);
        }

        var decision = _reminderPolicy.Evaluate(state, evaluation.Progress, now);
        _reminderStates[ruleId] = decision.NextState;
        return decision;
    }

    private ForceCloseDecision EvaluateForceClose(
        int ruleId,
        LimitEvaluation evaluation,
        bool isRunning,
        DateTimeOffset now)
    {
        _forceCloseDeadlines.TryGetValue(ruleId, out var existingDeadline);
        var decision = _forceClosePolicy.Evaluate(
            evaluation,
            now,
            _forceCloseDeadlines.ContainsKey(ruleId) ? existingDeadline : null,
            isRunning);

        if (decision.State is ForceCloseCountdownState.NotRequired or ForceCloseCountdownState.Cancelled)
        {
            _forceCloseDeadlines.Remove(ruleId);
        }
        else if (decision.Deadline is not null)
        {
            _forceCloseDeadlines[ruleId] = decision.Deadline.Value;
        }

        return decision;
    }

    private static bool IsRuleTargetRunning(LimitRule rule, IReadOnlySet<int> runningAppIds)
    {
        return rule.Scope switch
        {
            LimitScope.Game => rule.AppId is not null && runningAppIds.Contains(rule.AppId.Value),
            LimitScope.Global => runningAppIds.Count > 0,
            _ => false
        };
    }
}

public sealed record RuntimeLimitCheckResult(IReadOnlyList<RuntimeLimitDecision> Decisions)
{
    public bool HasReminder => Decisions.Any(decision => decision.Reminder.ShouldNotify);

    public bool HasForceCloseCountdown => Decisions.Any(decision => decision.ForceClose.ShouldStartCountdown);

    public bool HasReadyToClose => Decisions.Any(decision => decision.ForceClose.ShouldCloseNow);
}

public sealed record RuntimeLimitDecision(
    int RuleId,
    LimitEvaluation Evaluation,
    ReminderDecision Reminder,
    ForceCloseDecision ForceClose);

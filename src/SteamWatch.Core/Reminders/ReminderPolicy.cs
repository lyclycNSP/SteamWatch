namespace SteamWatch.Core.Reminders;

public sealed class ReminderPolicy
{
    private readonly IReadOnlyDictionary<ReminderLevel, double> _thresholds;

    private static readonly IReadOnlyDictionary<ReminderLevel, TimeSpan> Intervals =
        new Dictionary<ReminderLevel, TimeSpan>
        {
            [ReminderLevel.FirstWarning] = TimeSpan.FromMinutes(10),
            [ReminderLevel.SecondWarning] = TimeSpan.FromMinutes(5),
            [ReminderLevel.FinalWarning] = TimeSpan.FromMinutes(2),
            [ReminderLevel.Exceeded] = TimeSpan.FromMinutes(1)
        };

    public ReminderPolicy(ReminderThresholdSettings? thresholdSettings = null)
    {
        var settings = NormalizeThresholds(thresholdSettings ?? new ReminderThresholdSettings());
        _thresholds = new Dictionary<ReminderLevel, double>
        {
            [ReminderLevel.FirstWarning] = settings.FirstWarningPercent / 100.0,
            [ReminderLevel.SecondWarning] = settings.SecondWarningPercent / 100.0,
            [ReminderLevel.FinalWarning] = settings.FinalWarningPercent / 100.0,
            [ReminderLevel.Exceeded] = 1.00
        };
    }

    public ReminderLevel CalculateLevel(double progress)
    {
        if (progress >= _thresholds[ReminderLevel.Exceeded])
        {
            return ReminderLevel.Exceeded;
        }

        if (progress >= _thresholds[ReminderLevel.FinalWarning])
        {
            return ReminderLevel.FinalWarning;
        }

        if (progress >= _thresholds[ReminderLevel.SecondWarning])
        {
            return ReminderLevel.SecondWarning;
        }

        if (progress >= _thresholds[ReminderLevel.FirstWarning])
        {
            return ReminderLevel.FirstWarning;
        }

        return ReminderLevel.None;
    }

    public ReminderDecision Evaluate(ReminderState state, double progress, DateTimeOffset now)
    {
        var level = CalculateLevel(progress);
        if (level == ReminderLevel.None)
        {
            return ReminderDecision.NoReminder(level, state with { CurrentLevel = ReminderLevel.None });
        }

        var levelIncreased = level > state.CurrentLevel;
        var intervalElapsed = state.LastReminderAt is null
            || now - state.LastReminderAt.Value >= Intervals[level];

        if (!levelIncreased && !intervalElapsed)
        {
            return ReminderDecision.NoReminder(level, state);
        }

        var nextState = state with
        {
            CurrentLevel = level,
            LastReminderAt = now,
            ReminderCount = state.ReminderCount + 1
        };

        return new ReminderDecision(true, level, nextState);
    }

    private static ReminderThresholdSettings NormalizeThresholds(ReminderThresholdSettings settings)
    {
        var first = Math.Clamp(settings.FirstWarningPercent, 1, 99);
        var second = Math.Clamp(settings.SecondWarningPercent, first + 1, 99);
        var final = Math.Clamp(settings.FinalWarningPercent, second + 1, 99);
        return new ReminderThresholdSettings(first, second, final);
    }
}

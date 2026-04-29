namespace SteamWatch.Core.Reminders;

public sealed class ReminderPolicy
{
    private static readonly IReadOnlyDictionary<ReminderLevel, double> Thresholds =
        new Dictionary<ReminderLevel, double>
        {
            [ReminderLevel.FirstWarning] = 0.70,
            [ReminderLevel.SecondWarning] = 0.85,
            [ReminderLevel.FinalWarning] = 0.95,
            [ReminderLevel.Exceeded] = 1.00
        };

    private static readonly IReadOnlyDictionary<ReminderLevel, TimeSpan> Intervals =
        new Dictionary<ReminderLevel, TimeSpan>
        {
            [ReminderLevel.FirstWarning] = TimeSpan.FromMinutes(10),
            [ReminderLevel.SecondWarning] = TimeSpan.FromMinutes(5),
            [ReminderLevel.FinalWarning] = TimeSpan.FromMinutes(2),
            [ReminderLevel.Exceeded] = TimeSpan.FromMinutes(1)
        };

    public ReminderLevel CalculateLevel(double progress)
    {
        if (progress >= Thresholds[ReminderLevel.Exceeded])
        {
            return ReminderLevel.Exceeded;
        }

        if (progress >= Thresholds[ReminderLevel.FinalWarning])
        {
            return ReminderLevel.FinalWarning;
        }

        if (progress >= Thresholds[ReminderLevel.SecondWarning])
        {
            return ReminderLevel.SecondWarning;
        }

        if (progress >= Thresholds[ReminderLevel.FirstWarning])
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
}

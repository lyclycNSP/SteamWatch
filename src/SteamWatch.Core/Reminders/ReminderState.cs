namespace SteamWatch.Core.Reminders;

public sealed record ReminderState(
    int RuleId,
    ReminderLevel CurrentLevel = ReminderLevel.None,
    DateTimeOffset? LastReminderAt = null,
    int ReminderCount = 0);

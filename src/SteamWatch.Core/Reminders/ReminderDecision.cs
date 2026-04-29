namespace SteamWatch.Core.Reminders;

public sealed record ReminderDecision(
    bool ShouldNotify,
    ReminderLevel Level,
    ReminderState NextState)
{
    public static ReminderDecision NoReminder(ReminderLevel level, ReminderState state)
    {
        return new ReminderDecision(false, level, state);
    }
}

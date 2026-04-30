namespace SteamWatch.Core.Reminders;

public sealed record ReminderThresholdSettings(
    int FirstWarningPercent = 70,
    int SecondWarningPercent = 85,
    int FinalWarningPercent = 95);

namespace SteamWatch.Core.Settings;

public sealed record AppSettings(
    CloseWindowAction CloseWindowAction = CloseWindowAction.MinimizeToTray,
    bool StartWithWindows = false,
    bool PlayReminderSounds = true,
    int ForceCloseCountdownSeconds = 60,
    int FirstReminderThresholdPercent = 70,
    int SecondReminderThresholdPercent = 85,
    int FinalReminderThresholdPercent = 95);

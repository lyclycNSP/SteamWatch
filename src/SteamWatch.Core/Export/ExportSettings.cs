namespace SteamWatch.Core.Export;

public sealed record ExportSettings(
    string CloseAction,
    bool StartWithWindows,
    bool NotificationsEnabled,
    bool SoundEnabled);

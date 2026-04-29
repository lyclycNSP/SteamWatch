namespace SteamWatch.Core.Tracking;

public sealed record PlaytimeIncrement(
    int AppId,
    string GameName,
    int Minutes,
    bool IsFinal);

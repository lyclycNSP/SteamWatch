namespace SteamWatch.Core.Steam;

public sealed record SteamGameInfo(
    int AppId,
    string Name,
    int PlaytimeForeverMinutes,
    string? IconPath = null);

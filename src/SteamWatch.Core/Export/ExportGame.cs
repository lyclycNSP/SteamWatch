namespace SteamWatch.Core.Export;

public sealed record ExportGame(
    int AppId,
    string Name,
    int PlaytimeForeverMinutes,
    string? IconPath = null);

namespace SteamWatch.Core.Export;

public sealed record ExportPlaytimeRecord(
    DateOnly Date,
    int AppId,
    string GameName,
    int Minutes);

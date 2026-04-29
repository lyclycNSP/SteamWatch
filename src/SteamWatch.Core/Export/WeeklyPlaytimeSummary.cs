namespace SteamWatch.Core.Export;

public sealed record WeeklyPlaytimeSummary(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    int AppId,
    string GameName,
    int Minutes);

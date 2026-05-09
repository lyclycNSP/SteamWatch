namespace SteamWatch.Core.Tracking;

public sealed record SteamPlaytimeSnapshotGame(
    int AppId,
    string Name,
    int PlaytimeForeverMinutes);

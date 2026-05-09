namespace SteamWatch.Core.Tracking;

public sealed record SteamPlaytimeSnapshot(
    DateTimeOffset ObservedAt,
    IReadOnlyList<SteamPlaytimeSnapshotGame> Games);

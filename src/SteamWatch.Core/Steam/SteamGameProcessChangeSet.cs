namespace SteamWatch.Core.Steam;

public sealed record SteamGameProcessChangeSet(
    IReadOnlyList<RunningSteamGame> Started,
    IReadOnlyList<RunningSteamGame> Stopped,
    IReadOnlyList<RunningSteamGame> Running);

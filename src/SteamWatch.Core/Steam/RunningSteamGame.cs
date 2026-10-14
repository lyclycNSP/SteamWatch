namespace SteamWatch.Core.Steam;

public sealed record RunningSteamGame(
    int AppId,
    int ProcessId,
    string ProcessName,
    string? GameName = null,
    string? ExecutablePath = null,
    long? StartTimeUtcTicks = null);

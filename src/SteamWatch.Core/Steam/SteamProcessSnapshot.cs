namespace SteamWatch.Core.Steam;

public sealed record SteamProcessSnapshot(
    int ProcessId,
    int ParentProcessId,
    string Name,
    IReadOnlyList<string> CommandLine,
    string? ImagePath = null,
    long? StartTimeUtcTicks = null)
{
    public string ExecutablePath => ImagePath ?? CommandLine.FirstOrDefault() ?? string.Empty;
}

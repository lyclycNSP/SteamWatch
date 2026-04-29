namespace SteamWatch.Core.Steam;

public sealed record SteamProcessSnapshot(
    int ProcessId,
    int ParentProcessId,
    string Name,
    IReadOnlyList<string> CommandLine)
{
    public string ExecutablePath => CommandLine.FirstOrDefault() ?? string.Empty;
}

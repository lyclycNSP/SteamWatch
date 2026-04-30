namespace SteamWatch.Infrastructure.Enforcement;

public sealed record GameProcessTerminationResult(
    int ProcessId,
    bool CloseRequested,
    bool Killed,
    string? ErrorMessage = null)
{
    public bool IsSuccess => CloseRequested || Killed;
}

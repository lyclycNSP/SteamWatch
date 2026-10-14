namespace SteamWatch.Infrastructure.Enforcement;

public interface IProcessTerminatorBackend
{
    string? GetIdentity(int processId);
    bool IsSameProcess(int processId, string? identity);
    Task<bool> WaitForExitAsync(int processId, TimeSpan timeout, CancellationToken cancellationToken);

    bool TryCloseMainWindow(int processId);

    void KillProcessTree(int processId);
}

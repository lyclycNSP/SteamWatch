namespace SteamWatch.Infrastructure.Enforcement;

public interface IProcessTerminatorBackend
{
    bool TryCloseMainWindow(int processId);

    void KillProcessTree(int processId);
}

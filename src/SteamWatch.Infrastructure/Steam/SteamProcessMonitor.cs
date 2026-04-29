using SteamWatch.Core.Steam;

namespace SteamWatch.Infrastructure.Steam;

public sealed class SteamProcessMonitor
{
    private readonly IProcessSnapshotProvider _processSnapshotProvider;
    private readonly SteamGameProcessIdentifier _identifier;
    private readonly SteamGameProcessStateTracker _stateTracker = new();

    public SteamProcessMonitor(
        IProcessSnapshotProvider processSnapshotProvider,
        SteamGameProcessIdentifier identifier)
    {
        _processSnapshotProvider = processSnapshotProvider;
        _identifier = identifier;
    }

    public SteamGameProcessChangeSet Poll()
    {
        var snapshots = _processSnapshotProvider.GetProcesses();
        var runningGames = _identifier.Identify(snapshots);
        return _stateTracker.Update(runningGames);
    }
}

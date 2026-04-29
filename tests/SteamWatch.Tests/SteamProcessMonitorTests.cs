using SteamWatch.Core.Steam;
using SteamWatch.Infrastructure.Steam;

namespace SteamWatch.Tests;

[TestClass]
public sealed class SteamProcessMonitorTests
{
    [TestMethod]
    public void Poll_FirstGame_ReportsStarted()
    {
        var provider = new FakeProcessSnapshotProvider(
            [
                Process(10, 0, "steam.exe", "C:\\Steam\\steam.exe"),
                Process(20, 10, "cs2.exe", "C:\\Steam\\steamapps\\common\\Counter-Strike 2\\cs2.exe", "steam_app_730")
            ]);
        var identifier = new SteamGameProcessIdentifier([new SteamGameInfo(730, "Counter-Strike 2", 0)], @"C:\Steam");
        var monitor = new SteamProcessMonitor(provider, identifier);

        var changes = monitor.Poll();

        Assert.HasCount(1, changes.Started);
        Assert.AreEqual(730, changes.Started[0].AppId);
    }

    [TestMethod]
    public void Poll_GameStops_ReportsStopped()
    {
        var provider = new FakeProcessSnapshotProvider(
            [
                Process(10, 0, "steam.exe", "C:\\Steam\\steam.exe"),
                Process(20, 10, "cs2.exe", "C:\\Steam\\steamapps\\common\\Counter-Strike 2\\cs2.exe", "steam_app_730")
            ],
            [
                Process(10, 0, "steam.exe", "C:\\Steam\\steam.exe")
            ]);
        var identifier = new SteamGameProcessIdentifier([new SteamGameInfo(730, "Counter-Strike 2", 0)], @"C:\Steam");
        var monitor = new SteamProcessMonitor(provider, identifier);
        monitor.Poll();

        var changes = monitor.Poll();

        Assert.HasCount(1, changes.Stopped);
        Assert.AreEqual(730, changes.Stopped[0].AppId);
    }

    private static SteamProcessSnapshot Process(int pid, int ppid, string name, params string[] commandLine)
    {
        return new SteamProcessSnapshot(pid, ppid, name, commandLine);
    }

    private sealed class FakeProcessSnapshotProvider : IProcessSnapshotProvider
    {
        private readonly Queue<IReadOnlyList<SteamProcessSnapshot>> _snapshots;

        public FakeProcessSnapshotProvider(params IReadOnlyList<SteamProcessSnapshot>[] snapshots)
        {
            _snapshots = new Queue<IReadOnlyList<SteamProcessSnapshot>>(snapshots);
        }

        public IReadOnlyList<SteamProcessSnapshot> GetProcesses()
        {
            return _snapshots.Count > 0 ? _snapshots.Dequeue() : [];
        }
    }
}

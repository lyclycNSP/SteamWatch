using SteamWatch.Core.Steam;

namespace SteamWatch.Tests;

[TestClass]
public sealed class SteamGameProcessStateTrackerTests
{
    [TestMethod]
    public void Update_FirstRunningGame_ReportsStarted()
    {
        var tracker = new SteamGameProcessStateTracker();
        var game = new RunningSteamGame(730, 20, "cs2.exe", "Counter-Strike 2");

        var changes = tracker.Update([game]);

        Assert.HasCount(1, changes.Started);
        Assert.IsEmpty(changes.Stopped);
        Assert.HasCount(1, changes.Running);
    }

    [TestMethod]
    public void Update_SameGameStillRunning_ReportsNoChanges()
    {
        var tracker = new SteamGameProcessStateTracker();
        var game = new RunningSteamGame(730, 20, "cs2.exe", "Counter-Strike 2");
        tracker.Update([game]);

        var changes = tracker.Update([game]);

        Assert.IsEmpty(changes.Started);
        Assert.IsEmpty(changes.Stopped);
        Assert.HasCount(1, changes.Running);
    }

    [TestMethod]
    public void Update_GameStopped_ReportsStopped()
    {
        var tracker = new SteamGameProcessStateTracker();
        var game = new RunningSteamGame(730, 20, "cs2.exe", "Counter-Strike 2");
        tracker.Update([game]);

        var changes = tracker.Update([]);

        Assert.IsEmpty(changes.Started);
        Assert.HasCount(1, changes.Stopped);
        Assert.IsEmpty(changes.Running);
    }

    [TestMethod]
    public void Update_SwitchGames_ReportsOneStartedAndOneStopped()
    {
        var tracker = new SteamGameProcessStateTracker();
        var first = new RunningSteamGame(730, 20, "cs2.exe", "Counter-Strike 2");
        var second = new RunningSteamGame(570, 30, "dota2.exe", "Dota 2");
        tracker.Update([first]);

        var changes = tracker.Update([second]);

        Assert.HasCount(1, changes.Started);
        Assert.HasCount(1, changes.Stopped);
        Assert.AreEqual(570, changes.Started[0].AppId);
        Assert.AreEqual(730, changes.Stopped[0].AppId);
    }
}

using SteamWatch.Core.Steam;

namespace SteamWatch.Tests;

[TestClass]
public sealed class SteamGameProcessIdentifierTests
{
    [TestMethod]
    public void Identify_SteamChildWithSteamAppArgument_ReturnsRunningGame()
    {
        var processes = new[]
        {
            Process(10, 0, "steam.exe", "C:\\Steam\\steam.exe"),
            Process(20, 10, "cs2.exe", "C:\\Steam\\steamapps\\common\\Counter-Strike 2\\cs2.exe", "steam_app_730")
        };
        var identifier = new SteamGameProcessIdentifier([new SteamGameInfo(730, "Counter-Strike 2", 0)], @"C:\Steam");

        var games = identifier.Identify(processes);

        Assert.HasCount(1, games);
        Assert.AreEqual(730, games[0].AppId);
        Assert.AreEqual("Counter-Strike 2", games[0].GameName);
    }

    [TestMethod]
    public void Identify_OverlayTargetProcess_ReturnsRunningGame()
    {
        var processes = new[]
        {
            Process(10, 0, "steam.exe", "C:\\Steam\\steam.exe"),
            Process(30, 10, "gameoverlayui.exe", "C:\\Steam\\gameoverlayui.exe", "-pid", "40"),
            Process(40, 999, "dota2.exe", "C:\\Steam\\steamapps\\common\\dota 2 beta\\game\\bin\\win64\\dota2.exe", "steam_app_570")
        };
        var identifier = new SteamGameProcessIdentifier([new SteamGameInfo(570, "Dota 2", 0)], @"C:\Steam");

        var games = identifier.Identify(processes);

        Assert.HasCount(1, games);
        Assert.AreEqual(570, games[0].AppId);
    }

    [TestMethod]
    public void Identify_SteamLibraryPathWithoutSteamAppArgument_MatchesKnownGameByPath()
    {
        var processes = new[]
        {
            Process(10, 0, "steam.exe", "C:\\Steam\\steam.exe"),
            Process(20, 10, "CounterStrike2.exe", "C:\\Steam\\steamapps\\common\\Counter-Strike 2\\CounterStrike2.exe")
        };
        var identifier = new SteamGameProcessIdentifier([new SteamGameInfo(730, "Counter-Strike 2", 0)], @"C:\Steam");

        var games = identifier.Identify(processes);

        Assert.HasCount(1, games);
        Assert.AreEqual(730, games[0].AppId);
    }

    [TestMethod]
    public void Identify_SteamHelperProcess_IsIgnored()
    {
        var processes = new[]
        {
            Process(10, 0, "steam.exe", "C:\\Steam\\steam.exe"),
            Process(11, 10, "steamwebhelper.exe", "C:\\Steam\\steamwebhelper.exe", "steam_app_999")
        };
        var identifier = new SteamGameProcessIdentifier([new SteamGameInfo(999, "Not A Game", 0)], @"C:\Steam");

        var games = identifier.Identify(processes);

        Assert.IsEmpty(games);
    }

    [TestMethod]
    public void Identify_UnknownPathWithoutAppId_IsIgnored()
    {
        var processes = new[]
        {
            Process(10, 0, "steam.exe", "C:\\Steam\\steam.exe"),
            Process(20, 10, "unknown.exe", "C:\\Steam\\steamapps\\common\\Unknown\\unknown.exe")
        };
        var identifier = new SteamGameProcessIdentifier([new SteamGameInfo(730, "Counter-Strike 2", 0)], @"C:\Steam");

        var games = identifier.Identify(processes);

        Assert.IsEmpty(games);
    }

    [TestMethod]
    public void Identify_WhenSteamNotRunning_ReturnsEmpty()
    {
        var processes = new[]
        {
            Process(20, 10, "cs2.exe", "C:\\Steam\\steamapps\\common\\Counter-Strike 2\\cs2.exe", "steam_app_730")
        };
        var identifier = new SteamGameProcessIdentifier([new SteamGameInfo(730, "Counter-Strike 2", 0)], @"C:\Steam");

        var games = identifier.Identify(processes);

        Assert.IsEmpty(games);
    }

    [TestMethod]
    public void ExtractOverlayTargetProcessId_ReadsPidArgument()
    {
        var processId = SteamGameProcessIdentifier.ExtractOverlayTargetProcessId(["gameoverlayui.exe", "-pid", "1234"]);

        Assert.AreEqual(1234, processId);
    }

    private static SteamProcessSnapshot Process(int pid, int ppid, string name, params string[] commandLine)
    {
        return new SteamProcessSnapshot(pid, ppid, name, commandLine);
    }
}

using SteamWatch.Infrastructure.Steam;

namespace SteamWatch.Tests;

[TestClass]
public sealed class SteamCacheReaderTests
{
    [TestMethod]
    public async Task GetCurrentUserIdAsync_UsesMostRecentLoginUser()
    {
        var steamPath = CreateSteamFixture();
        Directory.CreateDirectory(Path.Combine(steamPath, "config"));
        await File.WriteAllTextAsync(
            Path.Combine(steamPath, "config", "loginusers.vdf"),
            """
            "users"
            {
                "76561198000000001"
                {
                    "MostRecent" "1"
                }
            }
            """);

        var reader = new SteamCacheReader(steamPath);
        var userId = await reader.GetCurrentUserIdAsync();

        Assert.AreEqual("39734273", userId);
    }

    [TestMethod]
    public async Task GetCurrentUserIdAsync_FallsBackToUserdataDirectory()
    {
        var steamPath = CreateSteamFixture();
        Directory.CreateDirectory(Path.Combine(steamPath, "userdata", "12345"));

        var reader = new SteamCacheReader(steamPath);
        var userId = await reader.GetCurrentUserIdAsync();

        Assert.AreEqual("12345", userId);
    }

    [TestMethod]
    public async Task ReadGamesAsync_ReadsManifestsPlaytimesAndIcons()
    {
        var steamPath = CreateSteamFixture();
        var userId = "39734273";
        Directory.CreateDirectory(Path.Combine(steamPath, "steamapps"));
        Directory.CreateDirectory(Path.Combine(steamPath, "userdata", userId, "config"));
        Directory.CreateDirectory(Path.Combine(steamPath, "appcache", "librarycache"));

        await File.WriteAllTextAsync(
            Path.Combine(steamPath, "steamapps", "appmanifest_730.acf"),
            """
            "AppState"
            {
                "appid" "730"
                "name" "Counter-Strike 2"
            }
            """);
        await File.WriteAllTextAsync(
            Path.Combine(steamPath, "userdata", userId, "config", "localconfig.vdf"),
            """
            "Apps"
            {
                "730"
                {
                    "Playtime" "120"
                }
            }
            """);
        await File.WriteAllTextAsync(Path.Combine(steamPath, "appcache", "librarycache", "730_icon.png"), "fake");

        var games = await new SteamCacheReader(steamPath).ReadGamesAsync(userId);

        Assert.HasCount(1, games);
        Assert.AreEqual(730, games[0].AppId);
        Assert.AreEqual("Counter-Strike 2", games[0].Name);
        Assert.AreEqual(120, games[0].PlaytimeForeverMinutes);
        Assert.IsTrue(games[0].IconPath?.EndsWith("730_icon.png"));
    }

    [TestMethod]
    public async Task ReadGamesAsync_MissingSteamApps_ReturnsEmptyList()
    {
        var games = await new SteamCacheReader(CreateSteamFixture()).ReadGamesAsync("123");

        Assert.IsEmpty(games);
    }

    [TestMethod]
    public void GetCommonPaths_IncludesExpectedDefaultSteamPath()
    {
        CollectionAssert.Contains(
            SteamPathResolver.GetCommonPaths().ToArray(),
            @"C:\Program Files (x86)\Steam");
    }

    private static string CreateSteamFixture()
    {
        var path = Path.Combine(Path.GetTempPath(), "SteamWatch.Tests", Guid.NewGuid().ToString("N"), "Steam");
        Directory.CreateDirectory(path);
        return path;
    }
}

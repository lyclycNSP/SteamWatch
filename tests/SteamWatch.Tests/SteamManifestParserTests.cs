using SteamWatch.Infrastructure.Steam;

namespace SteamWatch.Tests;

[TestClass]
public sealed class SteamManifestParserTests
{
    [TestMethod]
    public void ParseAppManifest_ValidManifest_ReturnsEntry()
    {
        const string content = """
            "AppState"
            {
                "appid"     "730"
                "name"      "Counter-Strike 2"
            }
            """;

        var entry = new SteamManifestParser().ParseAppManifest(content);

        Assert.IsNotNull(entry);
        Assert.AreEqual(730, entry.AppId);
        Assert.AreEqual("Counter-Strike 2", entry.Name);
    }

    [TestMethod]
    public void ParseAppManifest_MissingName_ReturnsNull()
    {
        const string content = """
            "AppState"
            {
                "appid" "730"
            }
            """;

        var entry = new SteamManifestParser().ParseAppManifest(content);

        Assert.IsNull(entry);
    }

    [TestMethod]
    public void ParsePlaytimes_ReadsAppPlaytimePairs()
    {
        const string content = """
            "Software"
            {
                "Valve"
                {
                    "Steam"
                    {
                        "Apps"
                        {
                            "730"
                            {
                                "Playtime" "120"
                            }
                            "570"
                            {
                                "Playtime" "45"
                            }
                        }
                    }
                }
            }
            """;

        var playtimes = new SteamManifestParser().ParsePlaytimes(content);

        Assert.AreEqual(120, playtimes[730]);
        Assert.AreEqual(45, playtimes[570]);
    }

    [TestMethod]
    public void ParseMostRecentUserId32_ConvertsSteamId64()
    {
        const string content = """
            "users"
            {
                "76561198000000000"
                {
                    "AccountName" "old"
                    "MostRecent" "0"
                }
                "76561198000000001"
                {
                    "AccountName" "current"
                    "MostRecent" "1"
                }
            }
            """;

        var userId = new SteamManifestParser().ParseMostRecentUserId32(content);

        Assert.AreEqual("39734273", userId);
    }
}

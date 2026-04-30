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
    public void GetGameIconPath_UsesModernLibraryCacheDirectory()
    {
        var steamPath = CreateSteamFixture();
        var iconDirectory = Path.Combine(steamPath, "appcache", "librarycache", "367520", "hash");
        Directory.CreateDirectory(iconDirectory);
        File.WriteAllText(Path.Combine(iconDirectory, "library_capsule.jpg"), "fake");

        var iconPath = new SteamCacheReader(steamPath).GetGameIconPath(367520);

        Assert.IsNotNull(iconPath);
        Assert.EndsWith(Path.Combine("hash", "library_capsule.jpg"), iconPath);
    }

    [TestMethod]
    public void GetGameIconPath_PrefersCapsuleArtworkOverLogo()
    {
        var steamPath = CreateSteamFixture();
        var iconDirectory = Path.Combine(steamPath, "appcache", "librarycache", "367520");
        Directory.CreateDirectory(iconDirectory);
        File.WriteAllText(Path.Combine(iconDirectory, "logo.png"), "fake");
        File.WriteAllText(Path.Combine(iconDirectory, "library_capsule.jpg"), "fake");

        var iconPath = new SteamCacheReader(steamPath).GetGameIconPath(367520);

        Assert.IsNotNull(iconPath);
        Assert.EndsWith("library_capsule.jpg", iconPath);
    }

    [TestMethod]
    public void GetGameIconPath_PrefersAppInfoClientIconOverLibraryArtwork()
    {
        const string iconHash = "29492c0a65a3d03943a8f4ab786c93a95a150a56";
        var steamPath = CreateSteamFixture();
        var iconDirectory = Path.Combine(steamPath, "steam", "games");
        var libraryCacheDirectory = Path.Combine(steamPath, "appcache", "librarycache", "367520");
        Directory.CreateDirectory(iconDirectory);
        Directory.CreateDirectory(libraryCacheDirectory);
        File.WriteAllText(Path.Combine(iconDirectory, $"{iconHash}.ico"), "fake");
        File.WriteAllText(Path.Combine(libraryCacheDirectory, "library_capsule.jpg"), "fake");
        WriteAppInfoFixture(steamPath, 367520, iconHash);

        var iconPath = new SteamCacheReader(steamPath).GetGameIconPath(367520);

        Assert.IsNotNull(iconPath);
        Assert.EndsWith(Path.Combine("steam", "games", $"{iconHash}.ico"), iconPath);
    }

    [TestMethod]
    public void GetGameIconPath_FallsBackToLibraryArtworkWhenAppInfoIconIsMissing()
    {
        const string iconHash = "29492c0a65a3d03943a8f4ab786c93a95a150a56";
        var steamPath = CreateSteamFixture();
        var libraryCacheDirectory = Path.Combine(steamPath, "appcache", "librarycache", "367520");
        Directory.CreateDirectory(libraryCacheDirectory);
        File.WriteAllText(Path.Combine(libraryCacheDirectory, "library_capsule.jpg"), "fake");
        WriteAppInfoFixture(steamPath, 367520, iconHash);

        var iconPath = new SteamCacheReader(steamPath).GetGameIconPath(367520);

        Assert.IsNotNull(iconPath);
        Assert.EndsWith("library_capsule.jpg", iconPath);
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

    private static void WriteAppInfoFixture(string steamPath, int appId, string iconHash)
    {
        Directory.CreateDirectory(Path.Combine(steamPath, "appcache"));

        const uint magicV29 = 0x07564429;
        const uint universe = 1;
        const uint clientIconIndex = 361;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(magicV29);
        writer.Write(universe);
        writer.Write(0U);
        writer.Write(0U);

        using var record = new MemoryStream();
        using (var recordWriter = new BinaryWriter(record, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            recordWriter.Write((byte)1);
            recordWriter.Write(clientIconIndex);
            recordWriter.Write(System.Text.Encoding.UTF8.GetBytes(iconHash));
            recordWriter.Write((byte)0);
        }

        writer.Write(appId);
        writer.Write((uint)record.Length);
        writer.Write(record.ToArray());

        var stringTableOffset = checked((uint)stream.Position);
        stream.Position = sizeof(uint) * 2;
        writer.Write(stringTableOffset);
        stream.Position = stringTableOffset;

        writer.Write(clientIconIndex + 1);
        for (uint index = 0; index <= clientIconIndex; index++)
        {
            var key = index switch
            {
                0 => "appinfo",
                1 => "appid",
                4 => "name",
                clientIconIndex => "clienticon",
                _ => $"key{index}"
            };
            writer.Write(System.Text.Encoding.UTF8.GetBytes(key));
            writer.Write((byte)0);
        }

        File.WriteAllBytes(Path.Combine(steamPath, "appcache", "appinfo.vdf"), stream.ToArray());
    }
}

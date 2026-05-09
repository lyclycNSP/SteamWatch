using SteamWatch.Core.Tracking;
using SteamWatch.Infrastructure.Storage;

namespace SteamWatch.Tests;

[TestClass]
public sealed class SteamPlaytimeSnapshotStoreTests
{
    [TestMethod]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsSnapshot()
    {
        var store = new SteamPlaytimeSnapshotStore(new JsonFileStore(CreateTempDirectory()));
        var snapshot = new SteamPlaytimeSnapshot(
            new DateTimeOffset(2026, 5, 9, 8, 0, 0, TimeSpan.Zero),
            [
                new SteamPlaytimeSnapshotGame(730, "Counter-Strike 2", 100),
                new SteamPlaytimeSnapshotGame(570, "Dota 2", 50)
            ]);

        await store.SaveAsync(snapshot);
        var loaded = await store.LoadAsync();

        Assert.IsNotNull(loaded);
        Assert.AreEqual(snapshot.ObservedAt, loaded.ObservedAt);
        Assert.HasCount(2, loaded.Games);
        Assert.AreEqual(100, loaded.Games.Single(game => game.AppId == 730).PlaytimeForeverMinutes);
    }

    [TestMethod]
    public async Task LoadAsync_MissingFile_ReturnsNull()
    {
        var store = new SteamPlaytimeSnapshotStore(new JsonFileStore(CreateTempDirectory()));

        var loaded = await store.LoadAsync();

        Assert.IsNull(loaded);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "SteamWatch.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

using SteamWatch.Core.Tracking;
using SteamWatch.Infrastructure.Storage;

namespace SteamWatch.Tests;

[TestClass]
public sealed class PlaytimeRecordStoreTests
{
    [TestMethod]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsRecords()
    {
        var store = new PlaytimeRecordStore(new JsonFileStore(CreateTempDirectory()));
        var book = new PlaytimeRecordBook();
        book.Add(new DateOnly(2026, 4, 30), new PlaytimeIncrement(730, "Counter-Strike 2", 12, false));
        book.Add(new DateOnly(2026, 4, 30), new PlaytimeIncrement(570, "Dota 2", 8, false));

        await store.SaveAsync(book);
        var loaded = await store.LoadAsync();

        Assert.AreEqual(12, loaded.GetGameMinutes(new DateOnly(2026, 4, 30), 730));
        Assert.AreEqual(8, loaded.GetGameMinutes(new DateOnly(2026, 4, 30), 570));
        Assert.AreEqual(20, loaded.GetTotalMinutes(new DateOnly(2026, 4, 30)));
    }

    [TestMethod]
    public async Task LoadAsync_MissingFile_ReturnsEmptyBook()
    {
        var store = new PlaytimeRecordStore(new JsonFileStore(CreateTempDirectory()));

        var loaded = await store.LoadAsync();

        Assert.IsEmpty(loaded.Records);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "SteamWatch.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

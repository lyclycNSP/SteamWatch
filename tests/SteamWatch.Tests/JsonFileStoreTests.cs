using SteamWatch.Infrastructure.Storage;

namespace SteamWatch.Tests;

[TestClass]
public sealed class JsonFileStoreTests
{
    [TestMethod]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsValue()
    {
        var store = new JsonFileStore(CreateTempDirectory());
        var value = new SampleSettings("最小化到托盘", true, 120);

        await store.SaveAsync("settings", value);
        var loaded = await store.LoadAsync<SampleSettings>("settings");

        Assert.IsNotNull(loaded);
        Assert.AreEqual(value.CloseAction, loaded.CloseAction);
        Assert.AreEqual(value.StartWithWindows, loaded.StartWithWindows);
        Assert.AreEqual(value.GlobalDailyLimitMinutes, loaded.GlobalDailyLimitMinutes);
    }

    [TestMethod]
    public async Task LoadAsync_MissingFile_ReturnsNull()
    {
        var store = new JsonFileStore(CreateTempDirectory());

        var loaded = await store.LoadAsync<SampleSettings>("missing");

        Assert.IsNull(loaded);
    }

    [TestMethod]
    public async Task SaveAsync_ExistingFile_ReplacesContent()
    {
        var store = new JsonFileStore(CreateTempDirectory());

        await store.SaveAsync("settings", new SampleSettings("退出应用", false, 30));
        await store.SaveAsync("settings", new SampleSettings("最小化到托盘", true, 90));
        var loaded = await store.LoadAsync<SampleSettings>("settings");

        Assert.IsNotNull(loaded);
        Assert.AreEqual("最小化到托盘", loaded.CloseAction);
        Assert.AreEqual(90, loaded.GlobalDailyLimitMinutes);
    }

    [TestMethod]
    public void Delete_ExistingFile_RemovesFile()
    {
        var store = new JsonFileStore(CreateTempDirectory());
        File.WriteAllText(store.GetPath("settings"), "{}");

        store.Delete("settings");

        Assert.IsFalse(store.Exists("settings"));
    }

    [TestMethod]
    public void Constructor_EmptyDirectory_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new JsonFileStore(""));
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "SteamWatch.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed record SampleSettings(
        string CloseAction,
        bool StartWithWindows,
        int GlobalDailyLimitMinutes);
}

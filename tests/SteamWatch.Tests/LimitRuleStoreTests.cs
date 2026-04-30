using SteamWatch.Core.Limits;
using SteamWatch.Infrastructure.Storage;

namespace SteamWatch.Tests;

[TestClass]
public sealed class LimitRuleStoreTests
{
    [TestMethod]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsRules()
    {
        var store = new LimitRuleStore(new JsonFileStore(CreateTempDirectory()));
        var rules = new[]
        {
            new LimitRule(LimitScope.Game, LimitPeriod.Day, 90, EnforcementMode.NotifyOnly, 730, "CS2"),
            new LimitRule(LimitScope.Global, LimitPeriod.Week, 600, EnforcementMode.ForceClose, null, "全部游戏")
        };

        await store.SaveAsync(rules);
        var loaded = await store.LoadAsync();

        Assert.HasCount(2, loaded);
        Assert.AreEqual(LimitScope.Game, loaded[0].Scope);
        Assert.AreEqual(730, loaded[0].AppId);
        Assert.AreEqual(EnforcementMode.ForceClose, loaded[1].Enforcement);
    }

    [TestMethod]
    public async Task LoadAsync_MissingFile_ReturnsEmptyList()
    {
        var store = new LimitRuleStore(new JsonFileStore(CreateTempDirectory()));

        var loaded = await store.LoadAsync();

        Assert.IsEmpty(loaded);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "SteamWatch.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

using SteamWatch.Infrastructure.Startup;

namespace SteamWatch.Tests;

[TestClass]
public sealed class WindowsStartupManagerTests
{
    [TestMethod]
    public void Enable_WritesQuotedExecutablePath()
    {
        var registry = new FakeStartupRegistry();
        var manager = new WindowsStartupManager(registry);

        manager.Enable(@"D:\Apps\SteamWatch\SteamWatch.exe");

        Assert.AreEqual("\"D:\\Apps\\SteamWatch\\SteamWatch.exe\"", registry.Values[WindowsStartupManager.DefaultAppName]);
    }

    [TestMethod]
    public void Disable_RemovesRegistryValue()
    {
        var registry = new FakeStartupRegistry();
        var manager = new WindowsStartupManager(registry);
        manager.Enable(@"D:\Apps\SteamWatch\SteamWatch.exe");

        manager.Disable();

        Assert.IsFalse(registry.Values.ContainsKey(WindowsStartupManager.DefaultAppName));
    }

    [TestMethod]
    public void GetState_WhenCommandExists_ReturnsEnabled()
    {
        var registry = new FakeStartupRegistry();
        registry.Write(WindowsStartupManager.DefaultAppName, "\"SteamWatch.exe\"");
        var manager = new WindowsStartupManager(registry);

        var state = manager.GetState();

        Assert.IsTrue(state.IsEnabled);
        Assert.AreEqual("\"SteamWatch.exe\"", state.Command);
    }

    [TestMethod]
    public void GetState_WhenMissing_ReturnsDisabled()
    {
        var state = new WindowsStartupManager(new FakeStartupRegistry()).GetState();

        Assert.IsFalse(state.IsEnabled);
        Assert.IsNull(state.Command);
    }

    [TestMethod]
    public void QuoteExecutablePath_AlreadyQuoted_DoesNotDoubleQuote()
    {
        var quoted = WindowsStartupManager.QuoteExecutablePath("\"SteamWatch.exe\"");

        Assert.AreEqual("\"SteamWatch.exe\"", quoted);
    }

    private sealed class FakeStartupRegistry : IStartupRegistry
    {
        public Dictionary<string, string> Values { get; } = [];

        public string? Read(string name)
        {
            return Values.GetValueOrDefault(name);
        }

        public void Write(string name, string command)
        {
            Values[name] = command;
        }

        public void Delete(string name)
        {
            Values.Remove(name);
        }
    }
}

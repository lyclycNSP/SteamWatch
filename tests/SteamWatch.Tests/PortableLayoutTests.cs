using SteamWatch.Infrastructure.Storage;

namespace SteamWatch.Tests;

[TestClass]
public sealed class PortableLayoutTests
{
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "SteamWatch.Tests", Guid.NewGuid().ToString("N"));
    private string Local => Path.Combine(_workspace, "local");

    private string CreatePortable(string name, bool nested)
    {
        var root = Path.Combine(_workspace, name);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "portable.marker"), "fixture");
        File.WriteAllText(Path.Combine(root, "SteamWatch.exe"), "fixture");
        var app = nested ? Path.Combine(root, "app") : root;
        Directory.CreateDirectory(app);
        if (nested) File.WriteAllText(Path.Combine(app, "SteamWatch.exe"), "fixture");
        return root;
    }

    [TestMethod]
    public void NestedPayloadUsesRootDataAndRootLauncher()
    {
        var root = CreatePortable("中文 绿色版", true);
        var app = Path.Combine(root, "app");
        Assert.AreEqual(Path.Combine(root, "data"), AppDataDirectory.Resolve(app, Local));
        Assert.AreEqual(Path.Combine(root, "SteamWatch.exe"), AppDataDirectory.ResolveExecutablePath(Path.Combine(app, "SteamWatch.exe")));
        Assert.IsFalse(Directory.Exists(Path.Combine(app, "data")));
    }

    [TestMethod]
    public void OldFlatPortableKeepsItsDataAndExecutable()
    {
        var root = CreatePortable("flat", false);
        Assert.AreEqual(Path.Combine(root, "data"), AppDataDirectory.Resolve(root, Local));
        Assert.AreEqual(Path.Combine(root, "SteamWatch.exe"), AppDataDirectory.ResolveExecutablePath(Path.Combine(root, "SteamWatch.exe")));
    }

    [TestMethod]
    public void ExistingRootDataSurvivesPayloadReplacement()
    {
        var root = CreatePortable("upgrade", true);
        var data = AppDataDirectory.Resolve(Path.Combine(root, "app"), Local);
        File.WriteAllText(Path.Combine(data, "settings.json"), "original");
        File.WriteAllText(Path.Combine(root, "app", "SteamWatch.exe"), "updated fixture");
        Assert.AreEqual(data, AppDataDirectory.Resolve(Path.Combine(root, "app"), Local));
        Assert.AreEqual("original", File.ReadAllText(Path.Combine(data, "settings.json")));
    }

    [TestMethod]
    public void MisplacedLegacyDataIsCopiedWithoutOverwritingOrDeletingSource()
    {
        var root = CreatePortable("legacy", true);
        var app = Path.Combine(root, "app");
        var legacy = Path.Combine(app, "data");
        var data = Path.Combine(root, "data");
        Directory.CreateDirectory(legacy);
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "settings.json"), "current");
        File.WriteAllText(Path.Combine(legacy, "settings.json"), "old");
        File.WriteAllText(Path.Combine(legacy, "limits.json"), "legacy rules");
        Assert.AreEqual(data, AppDataDirectory.Resolve(app, Local));
        Assert.AreEqual("current", File.ReadAllText(Path.Combine(data, "settings.json")));
        Assert.AreEqual("legacy rules", File.ReadAllText(Path.Combine(data, "limits.json")));
        Assert.AreEqual("old", File.ReadAllText(Path.Combine(legacy, "settings.json")));
        File.WriteAllText(Path.Combine(legacy, "later.json"), "must not copy again");
        AppDataDirectory.Resolve(app, Local);
        Assert.IsFalse(File.Exists(Path.Combine(data, "later.json")));
    }

    [TestMethod]
    public void RelocatingEntirePortableFolderUsesNewRoot()
    {
        var root = CreatePortable("before", true);
        var data = AppDataDirectory.Resolve(Path.Combine(root, "app"), Local);
        File.WriteAllText(Path.Combine(data, "settings.json"), "preserved");
        var moved = Path.Combine(_workspace, "搬迁后 绿色版");
        Directory.Move(root, moved);
        Assert.AreEqual(Path.Combine(moved, "data"), AppDataDirectory.Resolve(Path.Combine(moved, "app"), Local));
        Assert.AreEqual("preserved", File.ReadAllText(Path.Combine(moved, "data", "settings.json")));
        Assert.AreEqual(Path.Combine(moved, "SteamWatch.exe"), AppDataDirectory.ResolveExecutablePath(Path.Combine(moved, "app", "SteamWatch.exe")));
    }

    [TestMethod]
    public void InstalledFlatLayoutKeepsLocalAppDataAndActualExecutable()
    {
        var installed = Path.Combine(Local, "Programs", "SteamWatch");
        Directory.CreateDirectory(installed);
        Assert.AreEqual(Path.Combine(Local, "SteamWatch"), AppDataDirectory.Resolve(installed, Local));
        Assert.AreEqual(Path.Combine(installed, "SteamWatch.exe"), AppDataDirectory.ResolveExecutablePath(Path.Combine(installed, "SteamWatch.exe")));
    }

    [TestMethod]
    public void UnmarkedAppFolderDoesNotUseParentData()
    {
        var app = Path.Combine(_workspace, "unmarked", "app");
        Directory.CreateDirectory(app);
        Assert.AreEqual(Path.Combine(app, "data"), AppDataDirectory.Resolve(app, Local));
        Assert.AreEqual(Path.Combine(app, "SteamWatch.exe"), AppDataDirectory.ResolveExecutablePath(Path.Combine(app, "SteamWatch.exe")));
    }
}

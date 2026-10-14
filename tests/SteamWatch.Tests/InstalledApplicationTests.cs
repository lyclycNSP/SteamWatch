using SteamWatch.Infrastructure.Storage;

namespace SteamWatch.Tests;

[TestClass]
public sealed class InstalledApplicationTests
{
    private static (string Local, string App, string Uninstall) Fixture()
    {
        var local = Path.Combine(Path.GetTempPath(), "SteamWatch.UninstallTests", Guid.NewGuid().ToString("N"));
        var app = Path.Combine(local, "Programs", "SteamWatch");
        var uninstall = Path.Combine(local, "Programs", "SteamWatch.Installers", Guid.NewGuid().ToString("N"), "unins000.exe");
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(Path.GetDirectoryName(uninstall)!);
        File.WriteAllText(uninstall, "test-only entry, never launched");
        return (local, app, uninstall);
    }

    [TestMethod]
    public void RegisteredInnoCommandResolvesOnlyForInstalledApplication()
    {
        var f = Fixture();
        var result = InstalledApplication.ResolveCommand(f.App, f.Local, '"' + f.Uninstall + '"');
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsInno);
        Assert.AreEqual(f.Uninstall, result.FileName);
        Assert.IsEmpty(result.Arguments);
        Assert.IsTrue(InstalledApplication.IsInstalled(f.App, f.Local));
        Assert.IsNull(InstalledApplication.ResolveCommand(Path.Combine(f.App, "app"), f.Local, '"' + f.Uninstall + '"'));
        File.WriteAllText(Path.Combine(f.App, "portable.marker"), "portable");
        Assert.IsNull(InstalledApplication.ResolveCommand(f.App, f.Local, '"' + f.Uninstall + '"'));
        Assert.IsFalse(InstalledApplication.IsInstalled(f.App, f.Local));
    }

    [TestMethod]
    [DataRow("suffix")]
    [DataRow("unquoted")]
    [DataRow("foreign")]
    [DataRow("nonguid")]
    public void ArbitraryRegistryCommandsAreNotExecuted(string kind)
    {
        var f = Fixture();
        var command = kind switch
        {
            "suffix" => '"' + f.Uninstall + "\" /arbitrary",
            "unquoted" => f.Uninstall,
            "foreign" => "\"C:\\Windows\\System32\\cmd.exe\"",
            _ => '"' + f.Uninstall.Replace(Path.GetFileName(Path.GetDirectoryName(f.Uninstall))!, "other") + '"'
        };
        Assert.IsNull(InstalledApplication.ResolveCommand(f.App, f.Local, command));
    }

    [TestMethod]
    public void OriginalScriptRemainsAvailableOnlyAtFixedOldInstallPath()
    {
        var f = Fixture();
        File.WriteAllText(Path.Combine(f.App, "Uninstall.ps1"), "old script, never executed");
        var old = InstalledApplication.ResolveCommand(f.App, f.Local, null);
        Assert.IsNotNull(old);
        Assert.IsFalse(old.IsInno);
        CollectionAssert.Contains(old.Arguments, "-CallerProcessId");
        var inno = InstalledApplication.ResolveCommand(f.App, f.Local, '"' + f.Uninstall + '"');
        Assert.IsTrue(inno!.IsInno);
        Assert.IsNull(InstalledApplication.ResolveCommand(Path.GetDirectoryName(f.App)!, f.Local, null));
    }
}

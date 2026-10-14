using System.IO.Compression;
using SteamWatch.Installer;

namespace SteamWatch.Tests;

[TestClass]
public sealed class InstallerPayloadTests
{
    private static (string Root, string Zip, string Stage) Fixture(params (string Name, string Contents)[] entries)
    {
        var root = Path.Combine(Path.GetTempPath(), "SteamWatch.PayloadTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var zipPath = Path.Combine(root, "payload.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            foreach (var (name, contents) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write(contents);
            }
        return (root, zipPath, Path.Combine(root, "stage"));
    }

    [TestMethod]
    public void FlatPayloadExtractsDependencies()
    {
        var fixture = Fixture(("SteamWatch-win-x64/SteamWatch.exe", "application"),
            ("SteamWatch-win-x64/nested/dependency.dll", "dependency"));
        PayloadExtractor.Extract(fixture.Zip, fixture.Stage);
        Assert.AreEqual("application", File.ReadAllText(Path.Combine(fixture.Stage, "SteamWatch.exe")));
        Assert.AreEqual("dependency", File.ReadAllText(Path.Combine(fixture.Stage, "nested", "dependency.dll")));
        Assert.IsFalse(File.Exists(Path.Combine(fixture.Stage, "portable.marker")));
    }

    [TestMethod]
    [DataRow("../escape.exe")]
    [DataRow("SteamWatch-win-x64/../escape.exe")]
    [DataRow("SteamWatch-win-x64/SteamWatch.exe:stream")]
    [DataRow("SteamWatch-win-x64/SteamWatch.exe.")]
    [DataRow("SteamWatch-win-x64/portable.marker")]
    [DataRow("SteamWatch-win-x64/Uninstall.ps1")]
    [DataRow("SteamWatch-win-x64/SteamWatchSetup.exe")]
    public void UnsafeWindowsPayloadNamesAreRejected(string name)
    {
        var fixture = Fixture((name, "unsafe"));
        Assert.ThrowsExactly<InvalidDataException>(() => PayloadExtractor.Extract(fixture.Zip, fixture.Stage));
        Assert.IsFalse(File.Exists(Path.Combine(fixture.Root, "escape.exe")));
    }

    [TestMethod]
    public void CaseInsensitiveDuplicateAndMissingEntryPointAreRejected()
    {
        var duplicate = Fixture(("SteamWatch-win-x64/SteamWatch.exe", "first"), ("SteamWatch-win-x64/STEAMWATCH.EXE", "second"));
        Assert.ThrowsExactly<InvalidDataException>(() => PayloadExtractor.Extract(duplicate.Zip, duplicate.Stage));
        var missing = Fixture(("SteamWatch-win-x64/dependency.dll", "dependency"));
        Assert.ThrowsExactly<InvalidDataException>(() => PayloadExtractor.Extract(missing.Zip, missing.Stage));
    }

    [TestMethod]
    public void SymbolicLinkPayloadEntryIsRejected()
    {
        var fixture = Fixture();
        using (var zip = ZipFile.Open(fixture.Zip, ZipArchiveMode.Update))
        {
            var entry = zip.CreateEntry("SteamWatch-win-x64/SteamWatch.exe");
            entry.ExternalAttributes = unchecked((int)0xA1FF0000);
            using var writer = new StreamWriter(entry.Open()); writer.Write("../outside");
        }
        Assert.ThrowsExactly<InvalidDataException>(() => PayloadExtractor.Extract(fixture.Zip, fixture.Stage));
        Assert.IsFalse(File.Exists(Path.Combine(fixture.Stage, "SteamWatch.exe")));
    }
}

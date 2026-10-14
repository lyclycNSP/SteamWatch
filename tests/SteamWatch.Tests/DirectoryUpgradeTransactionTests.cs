using System.Text.Json.Nodes;
using SteamWatch.Installer;

namespace SteamWatch.Tests;

[TestClass]
public sealed class DirectoryUpgradeTransactionTests
{
    private static (string Root, string Install, string Stage) Fixture(bool previous = true)
    {
        var root = Path.Combine(Path.GetTempPath(), "SteamWatch.TransactionTests", Guid.NewGuid().ToString("N"));
        var install = Path.Combine(root, "program");
        var stage = Path.Combine(root, "SteamWatch.staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        File.WriteAllText(Path.Combine(stage, "payload"), "new");
        if (previous) { Directory.CreateDirectory(install); File.WriteAllText(Path.Combine(install, "payload"), "old"); }
        return (root, install, stage);
    }

    [TestMethod]
    public void SwitchFailureAndRepeatedRecoveryPreserveBothPayloadsAndSnapshot()
    {
        var fixture = Fixture();
        using (var tx = new DirectoryUpgradeTransaction(fixture.Install))
        {
            tx.Prepare(fixture.Stage, "original registration");
            tx.SwitchDirectories();
        }
        using var reopened = new DirectoryUpgradeTransaction(fixture.Install);
        var restores = 0;
        reopened.Rollback(snapshot => { Assert.AreEqual("original registration", snapshot); restores++; });
        reopened.Rollback(_ => Assert.Fail("Completed recovery must not repeat external effects."));
        Assert.AreEqual(1, restores);
        Assert.AreEqual("old", File.ReadAllText(Path.Combine(fixture.Install, "payload")));
        Assert.AreEqual("new", File.ReadAllText(Path.Combine(fixture.Stage, "payload")));
        Assert.AreEqual("RolledBack", reopened.Phase);
    }

    [TestMethod]
    public void RegistrationRestoreFailureKeepsJournalForRetry()
    {
        var fixture = Fixture();
        using (var tx = new DirectoryUpgradeTransaction(fixture.Install))
        {
            tx.Prepare(fixture.Stage, "snapshot"); tx.SwitchDirectories();
            Assert.ThrowsExactly<IOException>(() => tx.Rollback(_ => throw new IOException("registry locked")));
            Assert.AreEqual("old", File.ReadAllText(Path.Combine(fixture.Install, "payload")));
            Assert.AreEqual("Switched", tx.Phase);
        }
        using var reopened = new DirectoryUpgradeTransaction(fixture.Install);
        reopened.Rollback(snapshot => Assert.AreEqual("snapshot", snapshot));
        Assert.AreEqual("RolledBack", reopened.Phase);
    }

    [TestMethod]
    public void NewInstallRollbackRetainsStagingWithoutInventingPreviousInstall()
    {
        var fixture = Fixture(previous: false);
        using var tx = new DirectoryUpgradeTransaction(fixture.Install);
        tx.Prepare(fixture.Stage, "absent"); tx.SwitchDirectories(); tx.Rollback(_ => { });
        Assert.IsFalse(Directory.Exists(fixture.Install));
        Assert.AreEqual("new", File.ReadAllText(Path.Combine(fixture.Stage, "payload")));
    }

    [TestMethod]
    public void CompletedUpgradeKeepsBackupAndRefusesAutomaticDowngrade()
    {
        var fixture = Fixture();
        string backup;
        using (var tx = new DirectoryUpgradeTransaction(fixture.Install))
        {
            tx.Prepare(fixture.Stage, "snapshot"); tx.SwitchDirectories(); tx.Commit(); backup = tx.BackupDirectory;
        }
        using var reopened = new DirectoryUpgradeTransaction(fixture.Install);
        Assert.ThrowsExactly<InvalidOperationException>(() => reopened.Rollback(_ => { }));
        Assert.AreEqual("new", File.ReadAllText(Path.Combine(fixture.Install, "payload")));
        Assert.AreEqual("old", File.ReadAllText(Path.Combine(backup, "payload")));
    }

    [TestMethod]
    public void ConcurrentWriterAndUnresolvedTransactionAreRejected()
    {
        var fixture = Fixture();
        using var tx = new DirectoryUpgradeTransaction(fixture.Install);
        Assert.ThrowsExactly<IOException>(() => new DirectoryUpgradeTransaction(fixture.Install));
        tx.Prepare(fixture.Stage, "snapshot");
        Assert.ThrowsExactly<InvalidOperationException>(() => tx.Prepare(fixture.Stage, "overwritten"));
    }

    [TestMethod]
    public void TraversalJournalAndForeignStagingAreRejectedBeforeMutation()
    {
        var fixture = Fixture();
        using (var tx = new DirectoryUpgradeTransaction(fixture.Install))
        {
            Assert.ThrowsExactly<ArgumentException>(() => tx.Prepare(Path.Combine(fixture.Stage, "nested"), "snapshot"));
            tx.Prepare(fixture.Stage, "snapshot");
        }
        var path = fixture.Install + ".upgrade.json";
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        json["StageName"] = "../outside";
        File.WriteAllText(path, json.ToJsonString());
        Assert.ThrowsExactly<InvalidDataException>(() => new DirectoryUpgradeTransaction(fixture.Install));
        Assert.AreEqual("old", File.ReadAllText(Path.Combine(fixture.Install, "payload")));
    }

    [TestMethod]
    public void ChangedInstalledIdentityIsNeverOverwrittenDuringRecovery()
    {
        var fixture = Fixture();
        using var tx = new DirectoryUpgradeTransaction(fixture.Install);
        tx.Prepare(fixture.Stage, "snapshot"); tx.SwitchDirectories();
        File.WriteAllText(Path.Combine(fixture.Install, ".steamwatch-install-transaction"), "foreign");
        Assert.ThrowsExactly<InvalidDataException>(() => tx.Rollback(_ => Assert.Fail("Must not restore registration on conflict.")));
        Assert.AreEqual("new", File.ReadAllText(Path.Combine(fixture.Install, "payload")));
        Assert.AreEqual("old", File.ReadAllText(Path.Combine(tx.BackupDirectory, "payload")));
    }

    [TestMethod]
    public void OnlyResolvedJournalCanBeArchivedBeforeNextUpgrade()
    {
        var fixture = Fixture();
        using var tx = new DirectoryUpgradeTransaction(fixture.Install);
        tx.Prepare(fixture.Stage, "first");
        Assert.ThrowsExactly<InvalidOperationException>(() => tx.ArchiveResolved());
        tx.SwitchDirectories(); tx.Commit();
        var archive = tx.ArchiveResolved();
        Assert.IsTrue(File.Exists(archive));
        Assert.IsFalse(File.Exists(fixture.Install + ".upgrade.json"));
        var next = Path.Combine(fixture.Root, "SteamWatch.staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(next); File.WriteAllText(Path.Combine(next, "payload"), "next");
        tx.Prepare(next, "second"); tx.SwitchDirectories(); tx.Rollback(_ => { });
        Assert.AreEqual("new", File.ReadAllText(Path.Combine(fixture.Install, "payload")));
        Assert.AreEqual("next", File.ReadAllText(Path.Combine(next, "payload")));
    }
}

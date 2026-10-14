using SteamWatch.Core.Notifications;
using SteamWatch.Core.Settings;
using SteamWatch.Core.Steam;
using SteamWatch.Core.Tracking;
using SteamWatch.Infrastructure.Enforcement;
using SteamWatch.Infrastructure.Notifications;
using SteamWatch.Infrastructure.Runtime;
using SteamWatch.Infrastructure.Startup;
using SteamWatch.Infrastructure.Steam;
using SteamWatch.Infrastructure.Storage;

namespace SteamWatch.Tests;

[TestClass]
public sealed class EstimatedPlaytimeTests
{
    [TestMethod]
    public void ReadSnapshot_EstimateOnlyDayRemainsVisibleButMeasuredProjectionExcludesIt()
    {
        var ledger = CreateLedger();
        ledger.ObserveSteam("account-A", [Game with { PlaytimeForeverMinutes = 120 }], Date.AddDays(1), Now.AddDays(1));
        var snapshot = ledger.CreateReadSnapshot();
        Assert.AreEqual(Date, snapshot.Records[0].Date);
        Assert.AreEqual(Date.AddDays(1), snapshot.Records[1].Date);
        Assert.AreEqual(17, snapshot.Records[0].GetGameMinutes(730));
        Assert.AreEqual(3, snapshot.Records[1].GetGameMinutes(730)); // Steam 120 - (observed 115 + measured 2).
        var measured = ledger.GetRecords(includeEstimates: false);
        Assert.AreEqual(2, measured[0].GetGameMinutes(730));
        Assert.AreEqual(0, measured[1].GetGameMinutes(730));
    }

    [TestMethod]
    public void ReadSnapshot_MatchesExportProjectionIncludingZeroCorrectionsAndSubMinuteDurations()
    {
        var ledger = CreateLedger();
        ledger.CorrectEstimatedMinutes(Date, 730, 15, 0, "fixture correction", Now);
        ledger.RecordInterval(570, "Second", null, Now, Now.AddSeconds(45), TimeSpan.FromSeconds(45));
        var exported = ledger.Export();
        var snapshot = ledger.CreateReadSnapshot();
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        Assert.AreEqual(System.Text.Json.JsonSerializer.Serialize(ledger.GetRecords(), options),
            System.Text.Json.JsonSerializer.Serialize(snapshot.Records, options));
        Assert.AreEqual(System.Text.Json.JsonSerializer.Serialize(exported.GameNames, options),
            System.Text.Json.JsonSerializer.Serialize(snapshot.GameNames, options));
        Assert.AreEqual(System.Text.Json.JsonSerializer.Serialize(exported.EstimatedEntries, options),
            System.Text.Json.JsonSerializer.Serialize(snapshot.EstimatedEntries, options));
        Assert.AreEqual(0, snapshot.EstimatedRecords.Single().GetGameMinutes(730));
        Assert.AreEqual(0, snapshot.Records.Single().GetGameMinutes(570));
        Assert.AreEqual(2, snapshot.Records.Single().GetGameMinutes(730));
    }

    [TestMethod]
    public void ReadSnapshot_CollectionsCannotMutateLedgerAndRemainDetachedFromLaterWrites()
    {
        var ledger = CreateLedger();
        var snapshot = ledger.CreateReadSnapshot();
        ledger.RecordInterval(730, "Renamed", null, Now, Now.AddMinutes(1), TimeSpan.FromMinutes(1));
        Assert.AreEqual(17, snapshot.Records.Single().GetGameMinutes(730));
        Assert.AreEqual("Fixture", snapshot.GameNames[730]);
        ((Dictionary<int, int>)snapshot.Records.Single().GameMinutes).Clear();
        ((Dictionary<int, int>)snapshot.EstimatedRecords.Single().GameMinutes).Clear();
        ((Dictionary<int, string>)snapshot.GameNames).Clear();
        ((List<EstimatedPlaytimeEntry>)snapshot.EstimatedEntries).Clear();
        var fresh = ledger.CreateReadSnapshot();
        Assert.AreEqual(18, fresh.Records.Single().GetGameMinutes(730));
        Assert.AreEqual(15, fresh.EstimatedRecords.Single().GetGameMinutes(730));
        Assert.AreEqual("Renamed", fresh.GameNames[730]);
        Assert.HasCount(1, fresh.EstimatedEntries);
    }

    private static readonly DateOnly Date = new(2026, 10, 11);
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.FromHours(8));
    private static readonly SteamGameInfo Game = new(730, "Fixture", 100);

    private static PlaytimeLedger CreateLedger()
    {
        var ledger = new PlaytimeLedger();
        ledger.ObserveSteam("account-A", [Game], Date, Now);
        ledger.ObserveSteam("account-A", [Game with { PlaytimeForeverMinutes = 115 }], Date, Now);
        ledger.RecordInterval(730, "Fixture", "account-A", Now, Now.AddMinutes(2), TimeSpan.FromMinutes(2));
        return ledger;
    }

    [TestMethod]
    public void DifferenceEntriesPreserveEvidenceAndAreIdempotent()
    {
        var ledger = CreateLedger();
        ledger.ObserveSteam("account-A", [Game with { PlaytimeForeverMinutes = 115 }], Date, Now);
        ledger.ObserveSteam("account-B", [Game with { PlaytimeForeverMinutes = 900 }], Date, Now);
        ledger.ObserveSteam("account-A", [Game], Date, Now);
        var entries = ledger.Export().EstimatedEntries!;
        Assert.HasCount(1, entries);
        Assert.AreEqual(15, entries[0].ChangeMinutes);
        Assert.AreEqual(100m, entries[0].SteamExpectedMinutes);
        Assert.AreEqual(115, entries[0].SteamObservedMinutes);
        Assert.AreEqual(Now, entries[0].RecordedAt);
    }

    [TestMethod]
    public void CorrectionSurvivesRestartWithoutRecreditingAndLeavesMeasuredTimeUnchanged()
    {
        var ledger = CreateLedger();
        ledger.CorrectEstimatedMinutes(Date, 730, 15, 0, "误记修正", Now);
        var restarted = new PlaytimeLedger(ledger.Export());
        Assert.AreEqual(0, restarted.ObserveSteam("account-A", [Game with { PlaytimeForeverMinutes = 117 }], Date, Now));
        Assert.AreEqual(2, restarted.GetRecords(false)[0].GetGameMinutes(730));
        Assert.AreEqual(2, restarted.GetRecords()[0].GetGameMinutes(730));
        Assert.AreEqual(3, restarted.ObserveSteam("account-A", [Game with { PlaytimeForeverMinutes = 120 }], Date, Now));
        Assert.AreEqual(3, restarted.GetEstimatedMinutes(Date, 730));
        Assert.HasCount(3, restarted.Export().EstimatedEntries!);
    }

    [TestMethod]
    public void CorrectionRejectsStaleOrInvalidInputWithoutChangingLedger()
    {
        var ledger = CreateLedger();
        Assert.ThrowsExactly<InvalidOperationException>(() => ledger.CorrectEstimatedMinutes(Date, 730, 14, 10, "reason", Now));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ledger.CorrectEstimatedMinutes(Date, 730, 15, -1, "reason", Now));
        Assert.ThrowsExactly<ArgumentException>(() => ledger.CorrectEstimatedMinutes(Date, 730, 15, 10, "  ", Now));
        Assert.ThrowsExactly<ArgumentException>(() => ledger.CorrectEstimatedMinutes(Date, 730, 15, 15, "reason", Now));
        Assert.AreEqual(15, ledger.GetEstimatedMinutes(Date, 730));
        Assert.HasCount(1, ledger.Export().EstimatedEntries!);
    }

    [TestMethod]
    public async Task V1MigrationPreservesOriginalAndLabelsUnknownHistory()
    {
        var store = NewStore();
        var old = new PlaytimeLedgerState(1,
            [new(Date, new() { [730] = TimeSpan.TicksPerMinute }, new() { [730] = 8 })], [], new() { [730] = "Fixture" });
        await store.SaveAsync("playtime-ledger", old);
        var ledger = await new PlaytimeLedgerStore(store).LoadAsync();
        Assert.AreEqual(2, ledger.Export().SchemaVersion);
        Assert.AreEqual(9, ledger.GetRecords()[0].GetGameMinutes(730));
        var entry = ledger.Export().EstimatedEntries!.Single();
        Assert.AreEqual(EstimatedPlaytimeSource.LegacyAggregate, entry.Source);
        Assert.IsNull(entry.RecordedAt);
        Assert.IsNull(entry.AccountId);
        var original = Directory.GetFiles(Path.GetDirectoryName(store.GetPath("playtime-ledger"))!, "playtime-ledger-before-v2-*.json").Single();
        var preserved = System.Text.Json.JsonSerializer.Deserialize<PlaytimeLedgerState>(await File.ReadAllTextAsync(original),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.AreEqual(1, preserved!.SchemaVersion);
        Assert.AreEqual(8, preserved.Days[0].EstimatedMinutes[730]);
        Assert.HasCount(1, (await new PlaytimeLedgerStore(store).LoadAsync()).Export().EstimatedEntries!);
    }

    [TestMethod]
    public async Task FailedV1MigrationLeavesOriginalReadableAndCanRetry()
    {
        var store = NewStore();
        await store.SaveAsync("playtime-ledger", new PlaytimeLedgerState(1,
            [new(Date, [], new() { [730] = 8 })], [], []));
        using (var locked = new FileStream(store.GetPath("playtime-ledger"), FileMode.Open, FileAccess.Read, FileShare.Read))
            await Assert.ThrowsExactlyAsync<IOException>(() => new PlaytimeLedgerStore(store).LoadAsync());
        Assert.AreEqual(1, (await store.LoadAsync<PlaytimeLedgerState>("playtime-ledger"))!.SchemaVersion);
        var retried = await new PlaytimeLedgerStore(store).LoadAsync();
        Assert.AreEqual(8, retried.GetEstimatedMinutes(Date, 730));
        Assert.HasCount(1, retried.Export().EstimatedEntries!);
    }

    [TestMethod]
    public void V2RejectsMissingOrInconsistentAuditEntries()
    {
        var state = CreateLedger().Export();
        Assert.ThrowsExactly<InvalidDataException>(() => new PlaytimeLedger(state with { EstimatedEntries = null }));
        Assert.ThrowsExactly<InvalidDataException>(() => new PlaytimeLedger(state with { EstimatedEntries = [] }));
        var entry = state.EstimatedEntries![0];
        Assert.ThrowsExactly<InvalidDataException>(() => new PlaytimeLedger(state with { EstimatedEntries = [entry with { PreviousMinutes = 4 }] }));
    }

    [TestMethod]
    public async Task RuntimeCorrectionBacksUpAndFailedSaveDoesNotPublishCandidate()
    {
        var store = NewStore();
        await new PlaytimeLedgerStore(store).SaveAsync(CreateLedger());
        var runtime = new SteamWatchRuntimeService(store, new EmptyCatalog(), new EmptyProcesses(), new Clock(),
            new Startup(), new Terminator(), new Notifications());
        await runtime.InitializeAsync();
        using (var locked = new FileStream(store.GetPath("playtime-ledger"), FileMode.Open, FileAccess.Read, FileShare.Read))
            await Assert.ThrowsExactlyAsync<IOException>(() => runtime.CorrectEstimatedMinutesAsync(Date, 730, 15, 5, "failure test"));
        Assert.AreEqual(15, runtime.Current.EstimatedRecords[0].GetGameMinutes(730));
        Assert.AreEqual(15, (await new PlaytimeLedgerStore(store).LoadAsync()).GetEstimatedMinutes(Date, 730));
        await runtime.CorrectEstimatedMinutesAsync(Date, 730, 15, 5, "corrected");
        Assert.AreEqual(5, runtime.Current.EstimatedRecords[0].GetGameMinutes(730));
        Assert.HasCount(2, runtime.Current.EstimatedEntries);
        var backups = Directory.GetFiles(Path.GetDirectoryName(store.GetPath("playtime-ledger"))!, "workspace-before-correction-*.json");
        Assert.HasCount(2, backups);
        var backup = System.Text.Json.JsonSerializer.Deserialize<SteamWatchBackup>(await runtime.CreateBackupAsync(),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        Assert.AreEqual(5, new PlaytimeLedger(backup.Ledger).GetEstimatedMinutes(Date, 730));
    }

    private static JsonFileStore NewStore() => new(Path.Combine(Path.GetTempPath(), "SteamWatch.Tests", Guid.NewGuid().ToString("N")));
    private sealed class Clock : IRuntimeClock { public DateTimeOffset Now => EstimatedPlaytimeTests.Now; public long ActiveTicks => 0; }
    private sealed class EmptyCatalog : ISteamCatalogProvider { public Task<SteamCatalog> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(new SteamCatalog("", null, [])); }
    private sealed class EmptyProcesses : IProcessSnapshotProvider { public IReadOnlyList<SteamProcessSnapshot> GetProcesses() => []; }
    private sealed class Startup : IStartupManager
    {
        public StartupState GetState() => new(false, null);
        public void Enable(string executablePath) { }
        public void Disable() { }
    }
    private sealed class Terminator : IGameProcessTerminator
    {
        public GameProcessTerminationResult Terminate(int processId) => throw new InvalidOperationException("No game may be closed in these tests.");
        public Task<GameProcessTerminationResult> TerminateAsync(int processId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("No game may be closed in these tests.");
    }
    private sealed class Notifications : INotificationService { public void Show(NotificationMessage message) { } }
}

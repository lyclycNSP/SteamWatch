using System.Text.Json;
using SteamWatch.Core.Limits;
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

/// <summary>Checked-in synthetic data exercises compatibility through production restore paths.</summary>
[TestClass]
public sealed class BaselineCompatibilityTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    private static readonly DateOnly Day = new(2026, 10, 11);

    [TestMethod]
    public async Task LegacyBackupWithoutCategoriesResetsClassificationAndPreservesExactTime()
    {
        var runtime = CreateRuntime();
        await runtime.RestoreBackupAsync(Read("backup-v2.json"));
        var result = await runtime.RestoreBackupAsync(Read("backup-without-categories.json"));
        Assert.HasCount(2, result.Rules);
        Assert.AreEqual(EnforcementMode.NotifyOnly, result.Rules.Single(r => r.Scope == LimitScope.Global).Enforcement);
        Assert.AreEqual(8, result.EstimatedRecords.Single().GetGameMinutes(730));
        Assert.AreEqual(EstimatedPlaytimeSource.LegacyAggregate, result.EstimatedEntries.Single().Source);
        Assert.IsNull(result.EstimatedEntries.Single().RecordedAt);
        var exported = JsonSerializer.Deserialize<SteamWatchBackup>(await runtime.CreateBackupAsync(), Options)!;
        Assert.IsEmpty(exported.CategoryOverrides!);
        Assert.AreEqual(900000000L, exported.Ledger.Days.Single().MeasuredTicks[730]);
        Assert.AreEqual(2, exported.Ledger.SchemaVersion);
    }

    [TestMethod]
    public async Task AuditedBackupRestoreRetainsCorrectionCursorAndPolicyAcrossRestart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SteamWatch.Tests", Guid.NewGuid().ToString("N"));
        var runtime = CreateRuntime(directory);
        await runtime.RestoreBackupAsync(Read("backup-v2.json"));
        var restarted = CreateRuntime(directory);
        var state = await restarted.InitializeAsync();
        Assert.AreEqual(5, state.EstimatedRecords.Single().GetGameMinutes(730));
        Assert.HasCount(2, state.EstimatedEntries);
        var backup = JsonSerializer.Deserialize<SteamWatchBackup>(await restarted.CreateBackupAsync(), Options)!;
        Assert.AreEqual(SteamAppCategory.Tool, backup.CategoryOverrides![730]);
        Assert.AreEqual(108m, backup.Ledger.SteamBaselines["synthetic-account"][730]);
        Assert.AreEqual(900000000L, backup.Ledger.Days.Single().MeasuredTicks[730]);
        Assert.AreEqual(EnforcementMode.ForceClose, backup.Rules.Single(r => r.Scope == LimitScope.Game).Enforcement);
        var ledger = new PlaytimeLedger(backup.Ledger);
        ledger.ObserveSteam("synthetic-account", [new(730, "Synthetic Game", 108)], Day,
            new(2026, 10, 11, 13, 0, 0, TimeSpan.FromHours(8)));
        Assert.AreEqual(5, ledger.GetEstimatedMinutes(Day, 730));
    }

    private static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "V2Baseline", name));

    private static SteamWatchRuntimeService CreateRuntime(string? directory = null) => new(
        new JsonFileStore(directory ?? Path.Combine(Path.GetTempPath(), "SteamWatch.Tests", Guid.NewGuid().ToString("N"))),
        new Catalog(), new Processes(), new Clock(), new Startup(), new Terminator(), new Notifications());

    private sealed class Clock : IRuntimeClock
    {
        public DateTimeOffset Now => new(2026, 10, 11, 12, 0, 0, TimeSpan.FromHours(8));
        public long ActiveTicks => 0;
    }
    private sealed class Catalog : ISteamCatalogProvider
    {
        public Task<SteamCatalog> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SteamCatalog("", null, []));
    }
    private sealed class Processes : IProcessSnapshotProvider
    {
        public IReadOnlyList<SteamProcessSnapshot> GetProcesses() => [];
    }
    private sealed class Startup : IStartupManager
    {
        public StartupState GetState() => new(false, null);
        public void Enable(string executablePath) => throw new InvalidOperationException("Fixture must not enable startup.");
        public void Disable() { } // Isolated adapter: never writes the user's registry.
    }
    private sealed class Terminator : IGameProcessTerminator
    {
        public GameProcessTerminationResult Terminate(int processId) => throw new InvalidOperationException("Fixture must not close processes.");
        public Task<GameProcessTerminationResult> TerminateAsync(int processId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Fixture must not close processes.");
    }
    private sealed class Notifications : INotificationService
    {
        public void Show(NotificationMessage message) { }
    }
}

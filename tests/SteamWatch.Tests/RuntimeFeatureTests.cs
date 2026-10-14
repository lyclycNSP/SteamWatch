using System.IO.Compression;
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

[TestClass]
public sealed class RuntimeFeatureTests
{
    [TestMethod]
    public async Task EarliestCloseTracksRuleChangesAndActiveTime()
    {
        var fixture = await Fixture.Create();
        var global = new LimitRule(LimitScope.Global, LimitPeriod.Day, 1, EnforcementMode.ForceClose, Name: "Global");
        var game = new LimitRule(LimitScope.Game, LimitPeriod.Day, 1, EnforcementMode.ForceClose, 730, "Game");
        await fixture.Runtime.SaveRuleAsync(global);
        fixture.Clock.Advance(10);
        await fixture.Runtime.PollAsync();
        await fixture.Runtime.SaveRuleAsync(game);
        StringAssert.Contains(fixture.Runtime.Current.EarliestForceCloseSummary, "Global");
        StringAssert.Contains(fixture.Runtime.Current.EarliestForceCloseSummary, "50 秒");
        fixture.Clock.ShiftWallClock(7200);
        await fixture.Runtime.PollAsync();
        StringAssert.Contains(fixture.Runtime.Current.EarliestForceCloseSummary, "50 秒");
        StringAssert.Contains(fixture.Runtime.Current.EarliestForceCloseSummary, "14:01:00");
        await fixture.Runtime.DeleteRuleAsync(global);
        StringAssert.Contains(fixture.Runtime.Current.EarliestForceCloseSummary, "Game");
        await fixture.Runtime.SaveRuleAsync(game with { Enforcement = EnforcementMode.NotifyOnly });
        StringAssert.Contains(fixture.Runtime.Current.EarliestForceCloseSummary, "暂无生效");
        await fixture.Runtime.SetPausedAsync(true);
        StringAssert.Contains(fixture.Runtime.Current.EarliestForceCloseSummary, "已暂停");
        await fixture.Runtime.SetPausedAsync(false);
        fixture.Processes.IsRunning = false;
        await fixture.Runtime.PollAsync();
        StringAssert.Contains(fixture.Runtime.Current.EarliestForceCloseSummary, "无运行目标");
    }

    [TestMethod]
    public async Task CategoriesPersistRestoreAndNeverChangeLimitsOrMeasuredTotals()
    {
        var fixture = await Fixture.Create();
        Assert.AreEqual(SteamAppCategory.Unknown, fixture.Runtime.Current.Games[0].Category);
        await fixture.Runtime.SetGameCategoryAsync(730, SteamAppCategory.Tool);
        var backup = await fixture.Runtime.CreateBackupAsync();
        await fixture.Runtime.SetGameCategoryAsync(730, SteamAppCategory.Game);
        await fixture.Runtime.RestoreBackupAsync(backup);
        await fixture.Runtime.RefreshGamesAsync();
        Assert.AreEqual(SteamAppCategory.Tool, fixture.Runtime.Current.Games[0].Category);
        Assert.AreEqual(2, fixture.Runtime.Current.Games[0].TodayMinutes);
        var stored = await fixture.Store.LoadAsync<Dictionary<int, SteamAppCategory>>("game-categories");
        Assert.AreEqual(SteamAppCategory.Tool, stored![730]);
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => fixture.Runtime.SetGameCategoryAsync(730, (SteamAppCategory)99));
        Assert.AreEqual(SteamAppCategory.Tool, fixture.Runtime.Current.Games[0].Category);
    }

    [TestMethod]
    public void DiagnosticExportUsesAllowlistAndExcludesPrivateFields()
    {
        var directory = Temp();
        var secret = "PRIVATE-CREDENTIAL-DO-NOT-EXPORT";
        File.WriteAllText(Path.Combine(directory, "settings.json"), secret);
        File.WriteAllText(Path.Combine(directory, "workspace-before-restore-private.json"), secret);
        File.WriteAllText(Path.Combine(directory, "diagnostics.log"),
            JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow, category = "runtime", error = "IOException", message = secret, path = @"C:\Users\PrivatePerson" }) + "\n"
            + JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow, category = secret, error = secret }) + "\n"
            + "malformed\n[]\n");
        var snapshot = RuntimeSnapshot.Empty with { Status = secret, SteamPath = directory, UserId = secret,
            Games = [new(new(730, secret, 0, secret, directory), false, 0, 0)] };
        using var stream = new MemoryStream(DiagnosticPackage.Create(directory, snapshot));
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        Assert.HasCount(2, zip.Entries);
        var text = string.Join("\n", zip.Entries.Select(entry =>
        {
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        }));
        Assert.DoesNotContain(secret, text);
        Assert.DoesNotContain("PrivatePerson", text);
        Assert.DoesNotContain(directory, text);
        StringAssert.Contains(text, "IOException");
        Assert.IsLessThan(1024 * 1024, stream.Length);
    }

    [TestMethod]
    public void DiagnosticExportHandlesMissingAndOversizedLogs()
    {
        var directory = Temp();
        var empty = ReadReport(DiagnosticPackage.Create(directory, RuntimeSnapshot.Empty));
        StringAssert.Contains(empty, "no-log");
        File.WriteAllText(Path.Combine(directory, "diagnostics.log"), new string('x', 2 * 1024 * 1024) + "\n"
            + JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow, category = "monitor", error = "TimeoutException" }));
        var bytes = DiagnosticPackage.Create(directory, RuntimeSnapshot.Empty);
        StringAssert.Contains(ReadReport(bytes), "last-256-KiB");
        Assert.IsLessThan(1024 * 1024, bytes.Length);
    }

    private static string ReadReport(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("report.json")!.Open());
        return reader.ReadToEnd();
    }
    private static string Temp()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SteamWatch.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
    private sealed class Fixture
    {
        public JsonFileStore Store { get; } = new(Temp());
        public Clock Clock { get; } = new();
        public Processes Processes { get; } = new();
        public SteamWatchRuntimeService Runtime { get; }
        private Fixture() => Runtime = new(Store, new Catalog(), Processes, Clock, new Startup(), new Terminator(), new Notifications());
        public static async Task<Fixture> Create()
        {
            var fixture = new Fixture();
            var ledger = new PlaytimeLedger();
            ledger.RecordInterval(730, "Fixture", null, fixture.Clock.Now.AddMinutes(-2), fixture.Clock.Now, TimeSpan.FromMinutes(2));
            await new PlaytimeLedgerStore(fixture.Store).SaveAsync(ledger);
            await fixture.Runtime.RefreshGamesAsync();
            await fixture.Runtime.PollAsync();
            return fixture;
        }
    }
    private sealed class Clock : IRuntimeClock
    {
        public DateTimeOffset Now { get; private set; } = new(2026, 10, 11, 12, 0, 0, TimeSpan.FromHours(8));
        public long ActiveTicks { get; private set; }
        public void Advance(int seconds) { Now = Now.AddSeconds(seconds); ActiveTicks += TimeSpan.FromSeconds(seconds).Ticks; }
        public void ShiftWallClock(int seconds) => Now = Now.AddSeconds(seconds);
    }
    private sealed class Catalog : ISteamCatalogProvider
    {
        public Task<SteamCatalog> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(new SteamCatalog(@"C:\Steam", "A",
            [new(730, "Fixture", 100, InstallDirectory: @"C:\Steam\steamapps\common\Fixture")]));
    }
    private sealed class Processes : IProcessSnapshotProvider
    {
        public bool IsRunning { get; set; } = true;
        public IReadOnlyList<SteamProcessSnapshot> GetProcesses() => IsRunning
            ? [new(10, 0, "steam.exe", [@"C:\Steam\steam.exe"]), new(20, 10, "game.exe", [@"C:\Steam\steamapps\common\Fixture\game.exe"])] : [];
    }
    private sealed class Startup : IStartupManager
    {
        public StartupState GetState() => new(false, null);
        public void Enable(string executablePath) { }
        public void Disable() { }
    }
    private sealed class Terminator : IGameProcessTerminator
    {
        public GameProcessTerminationResult Terminate(int processId) => throw new InvalidOperationException("Unexpected close.");
        public Task<GameProcessTerminationResult> TerminateAsync(int processId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unexpected close.");
    }
    private sealed class Notifications : INotificationService { public void Show(NotificationMessage message) { } }
}

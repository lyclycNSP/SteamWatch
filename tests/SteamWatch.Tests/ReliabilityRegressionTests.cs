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
public sealed class ReliabilityRegressionTests
{
    [TestMethod]
    public async Task Runtime_ActiveLockedLedgerDefersReminderAndForceCloseUntilDurableRecovery()
    {
        var fixture = await Fixture.Create();
        var messages = new List<NotificationMessage>();
        fixture.Runtime.UserNotificationRaised += (_, message) => messages.Add(message);
        await fixture.Runtime.SaveRuleAsync(new(LimitScope.Game, LimitPeriod.Day, 1,
            EnforcementMode.ForceClose, 730, "Active recovery"));
        fixture.Clock.Advance(59);
        using (var locked = new FileStream(fixture.Store.GetPath("playtime-ledger"), FileMode.Open,
            FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAsync<IOException>(() => fixture.Runtime.FlushAsync());
            fixture.Clock.Advance(2);
            await Assert.ThrowsAsync<IOException>(() => fixture.Runtime.PollAsync());
            Assert.IsEmpty(messages);
            Assert.AreEqual(0, fixture.Terminator.Calls);
        }
        await fixture.Runtime.PollAsync();
        Assert.HasCount(2, messages); // Exceeded reminder and initial countdown.
        await fixture.Runtime.PollAsync();
        Assert.HasCount(2, messages);
        fixture.Clock.Advance(60);
        using (var locked = new FileStream(fixture.Store.GetPath("playtime-ledger"), FileMode.Open,
            FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAsync<IOException>(() => fixture.Runtime.PollAsync());
            Assert.AreEqual(0, fixture.Terminator.Calls);
        }
        await fixture.Runtime.PollAsync();
        Assert.AreEqual(1, fixture.Terminator.Calls);
        Assert.HasCount(3, messages); // The existing exceeded policy repeats once after 60 seconds.
        await fixture.Runtime.PollAsync();
        Assert.HasCount(3, messages); // An immediate retry must not repeat that reminder.
        await fixture.Runtime.FlushAsync();
        var ledger = await new PlaytimeLedgerStore(fixture.Store).LoadAsync();
        Assert.AreEqual(TimeSpan.FromSeconds(121).Ticks, ledger.Export().Days.Single().MeasuredTicks[730]);
    }

    [TestMethod]
    public async Task Store_CanceledStreamingSavePreservesPrimaryAndBackupAndAllowsRetry()
    {
        var directory = Temp();
        var store = new JsonFileStore(directory);
        await store.SaveAsync("value", "first");
        await store.SaveAsync("value", "second");
        var primary = await File.ReadAllBytesAsync(store.GetPath("value"));
        var backup = await File.ReadAllBytesAsync(store.GetPath("value") + ".bak");
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAsync<OperationCanceledException>(() => store.SaveAsync("value",
            new CancelingValue(cancellation), cancellation.Token));
        CollectionAssert.AreEqual(primary, await File.ReadAllBytesAsync(store.GetPath("value")));
        CollectionAssert.AreEqual(backup, await File.ReadAllBytesAsync(store.GetPath("value") + ".bak"));
        Assert.IsEmpty(Directory.GetFiles(directory, "*.tmp"));
        await store.SaveAsync("value", "third");
        Assert.AreEqual("third", await store.LoadAsync<string>("value"));
    }

    [TestMethod]
    public async Task Store_SerializationFailureCleansTemporaryFileAndAllowsRetry()
    {
        var directory = Temp();
        var store = new JsonFileStore(directory);
        await store.SaveAsync("value", "preserved");
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync("value", new[] { double.NaN }));
        Assert.AreEqual("preserved", await store.LoadAsync<string>("value"));
        Assert.IsEmpty(Directory.GetFiles(directory, "*.tmp"));
        await store.SaveAsync("value", "retry");
        Assert.AreEqual("retry", await store.LoadAsync<string>("value"));
    }

    [TestMethod]
    public async Task Runtime_FailedExitFlushRetriesWithoutLosingOrDuplicatingMeasuredTime()
    {
        var fixture = await Fixture.Create();
        fixture.Clock.Advance(45);
        using (var locked = new FileStream(fixture.Store.GetPath("playtime-ledger"), FileMode.Open,
            FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAsync<IOException>(() => fixture.Runtime.FlushAsync());
        }
        fixture.Clock.Advance(15);
        await fixture.Runtime.FlushAsync();
        var ledger = await new PlaytimeLedgerStore(fixture.Store).LoadAsync();
        Assert.AreEqual(TimeSpan.FromSeconds(60).Ticks, ledger.Export().Days.Single().MeasuredTicks[730]);
        Assert.AreEqual(0, fixture.Terminator.Calls);
        Assert.IsEmpty(Directory.GetFiles(Path.GetDirectoryName(fixture.Store.GetPath("playtime-ledger"))!, "*.tmp"));
    }

    private sealed class CancelingValue(CancellationTokenSource cancellation)
    {
        public string Content { get { cancellation.Cancel(); return new string('x', 100_000); } }
    }

    private static readonly DateTimeOffset Start = new(2026, 10, 10, 23, 58, 0, TimeSpan.FromHours(8));
    private static readonly SteamGameInfo Game = new(730, "Fixture", 100, InstallDirectory: @"C:\Steam\steamapps\common\Fixture");

    [TestMethod]
    public void Coordinator_DisablingThenReenablingStartsFreshCountdown()
    {
        var coordinator = new RuntimeLimitCoordinator();
        var today = DateOnly.FromDateTime(Start.DateTime);
        var rule = new LimitRule(LimitScope.Game, LimitPeriod.Day, 1, EnforcementMode.ForceClose, 730);
        DailyPlaytimeRecord[] records = [new(today, new Dictionary<int, int> { [730] = 2 })];
        HashSet<int> running = [730];
        var initial = coordinator.Evaluate([rule], records, today, running, Start);
        coordinator.Evaluate([rule with { MaxMinutes = 0 }], records, today, running, Start.AddSeconds(10));
        var resumed = coordinator.Evaluate([rule], records, today, running, Start.AddSeconds(70));
        Assert.IsFalse(resumed.HasReadyToClose);
        Assert.IsTrue(resumed.HasForceCloseCountdown);
        Assert.IsTrue(resumed.Decisions[0].ForceClose.Deadline > initial.Decisions[0].ForceClose.Deadline);
    }

    [TestMethod]
    public void Ledger_ShortSessionsAccumulateWithoutRoundingUp()
    {
        var ledger = new PlaytimeLedger();
        for (var i = 0; i < 3; i++) ledger.RecordInterval(730, "Fixture", null, Start, Start.AddSeconds(20), TimeSpan.FromSeconds(20));
        Assert.AreEqual(1, ledger.GetRecords()[0].GetGameMinutes(730));
        Assert.AreEqual(TimeSpan.TicksPerMinute, ledger.Export().Days[0].MeasuredTicks[730]);
    }

    [TestMethod]
    public void Ledger_MidnightSplitsDates()
    {
        var ledger = new PlaytimeLedger();
        ledger.RecordInterval(730, "Fixture", null, Start, Start.AddMinutes(5), TimeSpan.FromMinutes(5));
        Assert.AreEqual(2, ledger.GetRecords()[0].GetGameMinutes(730));
        Assert.AreEqual(3, ledger.GetRecords()[1].GetGameMinutes(730));
    }

    [TestMethod]
    public void Ledger_SteamCacheRollbackDoesNotDuplicateLocalTime()
    {
        var ledger = new PlaytimeLedger();
        var today = DateOnly.FromDateTime(Start.DateTime);
        ledger.ObserveSteam("A", [Game], today);
        ledger.RecordInterval(730, "Fixture", "A", Start, Start.AddMinutes(10), TimeSpan.FromMinutes(10));
        Assert.AreEqual(0, ledger.ObserveSteam("A", [Game], today));
        Assert.AreEqual(0, ledger.ObserveSteam("A", [Game with { PlaytimeForeverMinutes = 110 }], today));
        Assert.AreEqual(10, ledger.GetRecords().Sum(r => r.GetGameMinutes(730)));
    }

    [TestMethod]
    public void Ledger_AccountSwitchAndRepeatedOfflineObservationAreIdempotent()
    {
        var ledger = new PlaytimeLedger();
        var date = DateOnly.FromDateTime(Start.DateTime);
        ledger.ObserveSteam("A", [Game], date);
        Assert.AreEqual(0, ledger.ObserveSteam("B", [Game with { PlaytimeForeverMinutes = 900 }], date));
        Assert.AreEqual(5, ledger.ObserveSteam("A", [Game with { PlaytimeForeverMinutes = 105 }], date));
        Assert.AreEqual(0, ledger.ObserveSteam("A", [Game with { PlaytimeForeverMinutes = 105 }], date));
        Assert.AreEqual(5, ledger.GetRecords()[0].GetGameMinutes(730));
        Assert.AreEqual(0, ledger.GetRecords(false)[0].GetGameMinutes(730));
    }

    [TestMethod]
    public void Ledger_ClockRollbackAndSleepUseOnlyActiveDuration()
    {
        var ledger = new PlaytimeLedger();
        ledger.RecordInterval(730, "Fixture", null, Start, Start.AddHours(-1), TimeSpan.FromSeconds(20));
        ledger.RecordInterval(730, "Fixture", null, Start, Start.AddHours(8), TimeSpan.FromSeconds(40));
        Assert.AreEqual(TimeSpan.TicksPerMinute, ledger.Export().Days.Sum(d => d.MeasuredTicks.GetValueOrDefault(730)));
    }

    [TestMethod]
    public async Task Store_ConcurrentInstancesNeverShareTemporaryFiles()
    {
        var directory = Temp();
        await Task.WhenAll(Enumerable.Range(0, 12).Select(i => new JsonFileStore(directory).SaveAsync("same", new string((char)('a' + i), 100_000))));
        var saved = await new JsonFileStore(directory).LoadAsync<string>("same");
        Assert.AreEqual(100_000, saved!.Length);
        Assert.IsTrue(saved.All(c => c == saved[0]));
        Assert.IsEmpty(Directory.GetFiles(directory, "*.tmp"));
    }

    [TestMethod]
    public async Task Store_CorruptionRestoresBackupAndKeepsEvidence()
    {
        var directory = Temp();
        var store = new JsonFileStore(directory);
        await store.SaveAsync("value", 1);
        await store.SaveAsync("value", 2);
        File.WriteAllText(store.GetPath("value"), "broken");
        Assert.AreEqual(1, await store.LoadAsync<int>("value"));
        Assert.IsNotNull(store.RecoveryWarning);
        Assert.HasCount(1, Directory.GetFiles(directory, "*.corrupt-*"));
    }

    [TestMethod]
    public async Task Store_LegacyMigrationKeepsMinutesAndOriginalFiles()
    {
        var store = new JsonFileStore(Temp());
        var date = new DateOnly(2026,10,10);
        await new PlaytimeRecordStore(store).SaveAsync(new PlaytimeRecordBook([new(date, new Dictionary<int,int>{{730,12}})]));
        var ledger = await new PlaytimeLedgerStore(store).LoadAsync();
        Assert.AreEqual(12, ledger.GetRecords()[0].GetGameMinutes(730));
        Assert.IsTrue(store.Exists("playtime"));
        Assert.IsTrue(store.Exists("playtime-ledger"));
    }

    [TestMethod]
    public async Task Runtime_RefreshKeepsPartialSessionAndFlushPersistsSeconds()
    {
        var fixture = await Fixture.Create();
        fixture.Clock.Advance(50);
        await fixture.Runtime.RefreshGamesAsync();
        fixture.Clock.Advance(30);
        await fixture.Runtime.PollAsync();
        Assert.AreEqual(1, fixture.Runtime.Current.Games[0].TodayMinutes);
        fixture.Clock.Advance(10);
        await fixture.Runtime.FlushAsync();
        var ledger = await new PlaytimeLedgerStore(fixture.Store).LoadAsync();
        Assert.AreEqual(TimeSpan.FromSeconds(90).Ticks, ledger.Export().Days[0].MeasuredTicks[730]);
    }

    [TestMethod]
    public async Task Runtime_PausedLimitsKeepAccurateTimeWithoutClosingGame()
    {
        var f = await Fixture.Create();
        await f.Runtime.SaveRuleAsync(new(LimitScope.Global, LimitPeriod.Day, 1, EnforcementMode.ForceClose));
        await f.Runtime.SetPausedAsync(true);
        f.Clock.Advance(120);
        await f.Runtime.PollAsync();
        Assert.AreEqual(2, f.Runtime.Current.Games[0].TodayMinutes);
        Assert.AreEqual(0, f.Terminator.Calls);
    }

    [TestMethod]
    public async Task Runtime_ConcurrentCommandsAreSerialized()
    {
        var f = await Fixture.Create();
        f.Clock.Advance(80);
        await Task.WhenAll(Enumerable.Range(0,6).Select(_ => f.Runtime.PollAsync()));
        var ledger = await new PlaytimeLedgerStore(f.Store).LoadAsync();
        Assert.AreEqual(TimeSpan.FromSeconds(80).Ticks, ledger.Export().Days[0].MeasuredTicks[730]);
    }

    [TestMethod]
    public async Task Runtime_GlobalRulesIgnoreSelectedGameId()
    {
        var f = await Fixture.Create();
        await f.Runtime.SaveRuleAsync(new(LimitScope.Global,LimitPeriod.Day,60,EnforcementMode.NotifyOnly,730));
        await f.Runtime.SaveRuleAsync(new(LimitScope.Global,LimitPeriod.Day,90,EnforcementMode.NotifyOnly,570));
        Assert.HasCount(1,f.Runtime.Current.Rules);
        Assert.IsNull(f.Runtime.Current.Rules[0].AppId);
    }

    [TestMethod]
    public void Coordinator_DeleteDoesNotTransferDeadline()
    {
        var coordinator = new RuntimeLimitCoordinator();
        var date = new DateOnly(2026,10,10);
        var a = new LimitRule(LimitScope.Game,LimitPeriod.Day,1,EnforcementMode.ForceClose,1);
        var b = new LimitRule(LimitScope.Game,LimitPeriod.Day,1,EnforcementMode.ForceClose,2);
        var records = new[] { new DailyPlaytimeRecord(date,new Dictionary<int,int>{{1,2},{2,2}}) };
        coordinator.Evaluate([a,b],records,date,new HashSet<int>{1},Start);
        var result = coordinator.Evaluate([b],records,date,new HashSet<int>{2},Start.AddSeconds(30));
        Assert.AreEqual(Start.AddSeconds(90), result.Decisions[0].ForceClose.Deadline);
    }

    [TestMethod]
    public void Identifier_FuzzyExecutableDoesNotMatchUnrelatedDirectory()
    {
        var identifier = new SteamGameProcessIdentifier([new(1,"Alpha",0),new(2,"Alphabet",0)],@"C:\Steam");
        var games = identifier.Identify([new(10,0,"steam.exe",[@"C:\Steam\steam.exe"]),
            new(20,10,"a.exe",[@"C:\Steam\steamapps\common\Unrelated\a.exe"])]);
        Assert.IsEmpty(games);
    }

    [TestMethod]
    public async Task Catalog_SecondaryLibraryUsesManifestInstallDirectory()
    {
        var steam = Temp();
        var secondary = Temp();
        Directory.CreateDirectory(Path.Combine(steam,"steamapps"));
        Directory.CreateDirectory(Path.Combine(secondary,"steamapps"));
        await File.WriteAllTextAsync(Path.Combine(steam,"steamapps","libraryfolders.vdf"), "\"libraryfolders\" { \"1\" { \"path\" \""+secondary.Replace("\\","\\\\")+"\" } }");
        await File.WriteAllTextAsync(Path.Combine(secondary,"steamapps","appmanifest_730.acf"),"\"AppState\" { \"appid\" \"730\" \"name\" \"Fixture\" \"installdir\" \"ActualFolder\" }");
        var games = await new SteamCacheReader(steam).ReadGamesAsync("fixture-user");
        Assert.HasCount(1,games);
        Assert.AreEqual(Path.Combine(secondary,"steamapps","common","ActualFolder"),games[0].InstallDirectory);
    }

    [TestMethod]
    public void DataDirectory_InstalledMigrationKeepsOriginalAndPortableStaysLocal()
    {
        var local = Temp();
        var app = Path.Combine(local,"Programs","SteamWatch");
        Directory.CreateDirectory(Path.Combine(app,"data"));
        File.WriteAllText(Path.Combine(app,"data","settings.json"),"{}");
        Assert.AreEqual(Path.Combine(local,"SteamWatch"),AppDataDirectory.Resolve(app,local));
        Assert.IsTrue(File.Exists(Path.Combine(app,"data","settings.json")));
        Assert.IsTrue(File.Exists(Path.Combine(local,"SteamWatch","settings.json")));
        File.WriteAllText(Path.Combine(app,"portable.marker"),"");
        Assert.AreEqual(Path.Combine(app,"data"),AppDataDirectory.Resolve(app,local));
    }

    [TestMethod]
    public async Task Runtime_GeneralSettingDoesNotRestartForceCloseDeadline()
    {
        var f = await Fixture.Create();
        await f.Runtime.SaveRuleAsync(new(LimitScope.Game,LimitPeriod.Day,1,EnforcementMode.ForceClose,730));
        f.Clock.Advance(60); await f.Runtime.PollAsync();
        f.Clock.Advance(30);
        await f.Runtime.SaveSettingsAsync(f.Runtime.Current.Settings with { PlayReminderSounds = false });
        f.Clock.Advance(30); await f.Runtime.PollAsync();
        Assert.AreEqual(1,f.Terminator.Calls);
    }

    [TestMethod]
    public async Task Runtime_SaveFailureRetriesWithoutDoubleCountingOrEarlyClose()
    {
        var f = await Fixture.Create();
        await f.Runtime.SaveRuleAsync(new(LimitScope.Global,LimitPeriod.Day,1,EnforcementMode.ForceClose));
        var file = f.Store.GetPath("playtime-ledger");
        File.Move(file,file+".held");
        Directory.CreateDirectory(file);
        f.Clock.Advance(60);
        await Assert.ThrowsExactlyAsync<IOException>(()=>f.Runtime.PollAsync());
        Assert.AreEqual(0,f.Terminator.Calls);
        Directory.Delete(file); // Empty fixture directory only.
        File.Move(file+".held",file);
        f.Clock.Advance(60);
        await f.Runtime.PollAsync();
        var ledger=await new PlaytimeLedgerStore(f.Store).LoadAsync();
        Assert.AreEqual(TimeSpan.FromSeconds(120).Ticks,ledger.Export().Days[0].MeasuredTicks[730]);
    }

    [TestMethod]
    public void Ledger_MissingPlaytimeDoesNotCreateZeroBaseline()
    {
        var ledger=new PlaytimeLedger();
        var date=new DateOnly(2026,10,10);
        ledger.ObserveSteam("A",[Game with { HasPlaytimeObservation=false, PlaytimeForeverMinutes=0 }],date);
        Assert.AreEqual(0,ledger.ObserveSteam("A",[Game],date));
        Assert.IsEmpty(ledger.GetRecords());
    }

    [TestMethod]
    public void Parser_NestedCloudBlockDoesNotHidePlaytime()
    {
        var parser=new SteamManifestParser();
        var values=parser.ParsePlaytimes("\"Apps\" { \"730\" { \"cloud\" { \"nested\" \"value\" } \"Playtime\" \"123\" } }");
        Assert.AreEqual(123,values[730]);
    }

    [TestMethod]
    public void Upgrade_RegistrationFailureRestoresOldDirectory()
    {
        var parent=Temp();
        var installed=Path.Combine(parent,"installed");
        var staging=Path.Combine(parent,"staging");
        Directory.CreateDirectory(installed); Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(installed,"version"),"old");
        File.WriteAllText(Path.Combine(staging,"version"),"new");
        Assert.ThrowsExactly<IOException>(()=>SteamWatch.Installer.DirectoryUpgrade.Commit(staging,installed,()=>throw new IOException("fixture")));
        Assert.AreEqual("old",File.ReadAllText(Path.Combine(installed,"version")));
        Assert.AreEqual("new",File.ReadAllText(Path.Combine(staging,"version")));
    }

    [TestMethod]
    public void Upgrade_SuccessRetainsLegacyDataInRecoveryDirectory()
    {
        var parent=Temp();
        var installed=Path.Combine(parent,"installed");
        var staging=Path.Combine(parent,"staging");
        Directory.CreateDirectory(installed); Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(installed,"user-data"),"keep");
        File.WriteAllText(Path.Combine(staging,"version"),"new");
        SteamWatch.Installer.DirectoryUpgrade.Commit(staging,installed,()=>{});
        Assert.AreEqual("new",File.ReadAllText(Path.Combine(installed,"version")));
        var backup=Directory.GetDirectories(parent,"SteamWatch.previous-*").Single();
        Assert.AreEqual("keep",File.ReadAllText(Path.Combine(backup,"user-data")));
    }

    [TestMethod]
    public void Coordinator_NewPeriodGetsNewCountdown()
    {
        var coordinator=new RuntimeLimitCoordinator();
        var first=new DateOnly(2026,10,10);
        var rule=new LimitRule(LimitScope.Game,LimitPeriod.Day,1,EnforcementMode.ForceClose,730);
        var records=new[]{ new DailyPlaytimeRecord(first,new Dictionary<int,int>{{730,2}}),new DailyPlaytimeRecord(first.AddDays(1),new Dictionary<int,int>{{730,2}}) };
        coordinator.Evaluate([rule],records,first,new HashSet<int>{730},Start);
        var result=coordinator.Evaluate([rule],records,first.AddDays(1),new HashSet<int>{730},Start.AddDays(1));
        Assert.AreEqual(Start.AddDays(1).AddSeconds(60),result.Decisions[0].ForceClose.Deadline);
    }

    [TestMethod]
    public async Task Runtime_BackupRestoreRetainsPreRestoreDataAndExactFractions()
    {
        var f=await Fixture.Create();
        f.Clock.Advance(30); await f.Runtime.PollAsync();
        var backup=await f.Runtime.CreateBackupAsync();
        f.Clock.Advance(90); await f.Runtime.PollAsync();
        await f.Runtime.RestoreBackupAsync(backup);
        var ledger=await new PlaytimeLedgerStore(f.Store).LoadAsync();
        Assert.AreEqual(TimeSpan.FromSeconds(30).Ticks,ledger.Export().Days[0].MeasuredTicks[730]);
        Assert.HasCount(1,Directory.GetFiles(Path.GetDirectoryName(f.Store.GetPath("playtime-ledger"))!,"workspace-before-restore-*.json"));
    }

    [TestMethod]
    public async Task Runtime_InvalidBackupDoesNotReplaceData()
    {
        var f=await Fixture.Create();
        f.Clock.Advance(90); await f.Runtime.PollAsync();
        await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>f.Runtime.RestoreBackupAsync("{\"schemaVersion\":999}"));
        var ledger=await new PlaytimeLedgerStore(f.Store).LoadAsync();
        Assert.AreEqual(TimeSpan.FromSeconds(90).Ticks,ledger.Export().Days[0].MeasuredTicks[730]);
    }

    [TestMethod]
    public async Task Runtime_ClockJumpDoesNotFinishCountdownEarly()
    {
        var f=await Fixture.Create();
        await f.Runtime.SaveRuleAsync(new(LimitScope.Global,LimitPeriod.Day,1,EnforcementMode.ForceClose));
        f.Clock.Advance(60); await f.Runtime.PollAsync();
        f.Clock.ShiftWallClock(3600); await f.Runtime.PollAsync();
        Assert.AreEqual(0,f.Terminator.Calls);
        f.Clock.Advance(60); await f.Runtime.PollAsync();
        Assert.AreEqual(1,f.Terminator.Calls);
    }

    [TestMethod]
    public async Task Runtime_InterruptedRestoreJournalIsReplayedDuringInitialization()
    {
        var source=await Fixture.Create();
        source.Clock.Advance(80); await source.Runtime.PollAsync();
        var backup=System.Text.Json.JsonSerializer.Deserialize<SteamWatchBackup>(await source.Runtime.CreateBackupAsync(),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        var destination=await Fixture.Create();
        await destination.Store.SaveAsync("workspace-restore",backup);
        var restarted=new SteamWatchRuntimeService(destination.Store,new FakeCatalog(),new FakeProcesses(),destination.Clock,
            new FakeStartup(),destination.Terminator,new FakeNotifications());
        await restarted.InitializeAsync();
        var ledger=await new PlaytimeLedgerStore(destination.Store).LoadAsync();
        Assert.AreEqual(TimeSpan.FromSeconds(80).Ticks,ledger.Export().Days[0].MeasuredTicks[730]);
        Assert.IsFalse(destination.Store.Exists("workspace-restore"));
    }

    [TestMethod]
    public async Task Store_NullDataCannotSilentlyBecomeDefaults()
    {
        var store=new JsonFileStore(Temp());
        File.WriteAllText(store.GetPath("settings"),"null");
        await Assert.ThrowsExactlyAsync<InvalidDataException>(()=>new AppSettingsStore(store).LoadAsync());
    }

    private static string Temp()
    {
        var path=Path.Combine(Path.GetTempPath(),"SteamWatch.Tests",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
    private sealed class Fixture
    {
        public JsonFileStore Store { get; } = new(Temp());
        public FakeClock Clock { get; } = new();
        public FakeTerminator Terminator { get; } = new();
        public SteamWatchRuntimeService Runtime { get; }
        private Fixture() => Runtime = new(Store,new FakeCatalog(),new FakeProcesses(),Clock,new FakeStartup(),Terminator,new FakeNotifications());
        public static async Task<Fixture> Create()
        {
            var f=new Fixture();
            await f.Runtime.RefreshGamesAsync(); await f.Runtime.PollAsync();
            return f;
        }
    }
    private sealed class FakeClock : IRuntimeClock
    {
        public DateTimeOffset Now { get; private set; } = new(2026,10,10,20,0,0,TimeSpan.FromHours(8));
        public long ActiveTicks { get; private set; }
        public void ShiftWallClock(int seconds) => Now = Now.AddSeconds(seconds);
        public void Advance(int seconds) { Now=Now.AddSeconds(seconds); ActiveTicks+=TimeSpan.FromSeconds(seconds).Ticks; }
    }
    private sealed class FakeCatalog : ISteamCatalogProvider
    {
        public Task<SteamCatalog> LoadAsync(CancellationToken cancellationToken=default) => Task.FromResult(new SteamCatalog(@"C:\Steam","A",new[]{Game}));
    }
    private sealed class FakeProcesses : IProcessSnapshotProvider
    {
        public IReadOnlyList<SteamProcessSnapshot> GetProcesses() => new SteamProcessSnapshot[]{new(10,0,"steam.exe",[@"C:\Steam\steam.exe"]),new(20,10,"game.exe",[@"C:\Steam\steamapps\common\Fixture\game.exe"])};
    }
    private sealed class FakeStartup : IStartupManager
    {
        public StartupState GetState()=>new(false,null);
        public void Enable(string executablePath) { }
        public void Disable() { }
    }
    private sealed class FakeTerminator : IGameProcessTerminator
    {
        public int Calls {get;private set;}
        public GameProcessTerminationResult Terminate(int processId) { Calls++; return new(processId,true,false,Exited:true); }
        public Task<GameProcessTerminationResult> TerminateAsync(int processId,CancellationToken cancellationToken=default)=>Task.FromResult(Terminate(processId));
    }
    private sealed class FakeNotifications : INotificationService { public void Show(NotificationMessage message) { } }
}

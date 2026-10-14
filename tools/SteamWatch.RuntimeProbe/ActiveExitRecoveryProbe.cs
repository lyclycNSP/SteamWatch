using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SteamWatch.App.Presentation;
using SteamWatch.App.Services;
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

namespace SteamWatch.RuntimeProbe;

internal static class ActiveExitRecoveryProbe
{
    private const int AppId = 1_234_567_890;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task RunAsync(string fixtureDirectory, string outputDirectory)
    {
        if (Directory.Exists(outputDirectory)) throw new IOException("Evidence directory must be new.");
        Directory.CreateDirectory(outputDirectory);
        var store = new JsonFileStore(Path.Combine(outputDirectory, "data"));
        var seed = new PlaytimeLedger(new(2,
            [new(DateOnly.FromDateTime(DateTime.Now), new() { [AppId] = 59 * TimeSpan.TicksPerSecond }, [])],
            [], new() { [AppId] = "Disposable active recovery fixture" }, []));
        await new PlaytimeLedgerStore(store).SaveAsync(seed);
        await new AppSettingsStore(store).SaveAsync(new(PlayReminderSounds: false,
            FirstReminderThresholdPercent: 40, SecondReminderThresholdPercent: 60, FinalReminderThresholdPercent: 80));
        await new LimitRuleStore(store).SaveAsync([new(LimitScope.Game, LimitPeriod.Day, 2,
            EnforcementMode.NotifyOnly, AppId, "Active recovery fixture")]);
        var gamePath = Path.Combine(fixtureDirectory, "steamapps", "common", "Fixture", "game.exe");
        var notifications = new Notifications();
        var runtime = new SteamWatchRuntimeService(store, new Catalog(fixtureDirectory, Path.GetDirectoryName(gamePath)!),
            new WindowsProcessSnapshotProvider(), new WindowsRuntimeClock(), new Startup(), new NoTermination(), notifications);
        var polls = 0;
        var failures = 0;
        var loops = 0;
        var activeLoops = 0;
        var overlap = false;
        var samples = new ConcurrentQueue<object>();
        var monitor = new BackgroundMonitor(async token =>
        {
            Interlocked.Increment(ref loops);
            if (Interlocked.Increment(ref activeLoops) != 1) overlap = true;
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    try
                    {
                        await runtime.PollAsync(token);
                        samples.Enqueue(new { at = DateTimeOffset.UtcNow, kind = "poll", minutes = runtime.Current.Games.Single().TodayMinutes });
                        Interlocked.Increment(ref polls);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                    catch (IOException ex)
                    {
                        samples.Enqueue(new { at = DateTimeOffset.UtcNow, kind = "save-failure", error = ex.GetType().Name });
                        Interlocked.Increment(ref failures);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            finally { Interlocked.Decrement(ref activeLoops); }
        });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var observation = Path.Combine(outputDirectory, "child.json");
        var start = new ProcessStartInfo(Path.Combine(fixtureDirectory, "steam.exe")) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in new[] { "launch-game", gamePath, "50", observation }) start.ArgumentList.Add(arg);
        using var parent = Process.Start(start) ?? throw new IOException("Fixture did not start.");
        var exit = new ExitPreparation();
        var steps = new List<string>();
        var passed = false;
        string? failure = null;
        double? measuredSeconds = null;
        double? expectedSeconds = null;
        long? reopenedTicks = null;
        try
        {
            await UntilAsync(() => File.Exists(observation), deadline.Token);
            await runtime.RefreshGamesAsync();
            await runtime.PollAsync();
            Require(runtime.Current.Games.Single().IsRunning, "Production WMI did not detect active fixture.");
            var before = await TicksAsync(store);
            var watch = Stopwatch.StartNew();
            monitor.Start();
            await Task.Delay(2100, deadline.Token);
            var original = await File.ReadAllBytesAsync(store.GetPath("playtime-ledger"), deadline.Token);
            await using (var fileLock = new FileStream(store.GetPath("playtime-ledger"), FileMode.Open,
                FileAccess.Read, FileShare.Read))
            {
                var rejected = false;
                try { await PrepareAsync(); }
                catch (IOException) { rejected = true; }
                Require(rejected && !exit.IsActive && !parent.HasExited, "Locked active flush must reject exit and release its guard.");
                steps.Add("active-flush-failure-rejected-exit");
                await UntilAsync(() => Volatile.Read(ref failures) >= 1, deadline.Token);
                Require(notifications.Messages.IsEmpty, "Failed persistence emitted a reminder.");
                var unchanged = await File.ReadAllBytesAsync(store.GetPath("playtime-ledger"), deadline.Token);
                Require(original.SequenceEqual(unchanged), "Failed flush replaced the ledger.");
                steps.Add("resumed-monitor-retried-locked-ledger-without-notifying");
            }
            await UntilAsync(() => Volatile.Read(ref polls) >= 2, deadline.Token);
            Require(await TicksAsync(store) > before, "Unlock did not automatically persist active ticks.");
            Require(notifications.Messages.Count == 1, "Expected one threshold reminder and no duplicate after recovery.");
            steps.Add("unlock-auto-persisted-ticks-and-delivered-one-reminder");

            using (var cancelled = new CancellationTokenSource())
            {
                var rejected = false;
                try
                {
                    await exit.TryPrepareAsync(() => Task.FromResult(true), () => Task.FromResult(true), async () =>
                    {
                        await monitor.StopAndFlushAsync(async () => await runtime.FlushAsync());
                        cancelled.Cancel();
                    }, monitor.Start, cancelled.Token);
                }
                catch (OperationCanceledException) { rejected = true; }
                Require(rejected && !exit.IsActive, "Cancellation after flush did not release preparation.");
                monitor.Start(); // Same duplicate resume calls used by the application must not create two loops.
                await UntilAsync(() => Volatile.Read(ref polls) >= 3, deadline.Token);
                Require(notifications.Messages.Count == 1 && !overlap, "Cancellation duplicated a loop or reminder.");
                steps.Add("cancel-after-flush-resumed-one-loop-without-duplicate-reminder");
            }
            Require(await PrepareAsync(), "Retry did not prepare exit.");
            expectedSeconds = watch.Elapsed.TotalSeconds;
            var finalTicks = await TicksAsync(store);
            measuredSeconds = (finalTicks - before) / (double)TimeSpan.TicksPerSecond;
            Require(Math.Abs(measuredSeconds.Value - expectedSeconds.Value) <= 2,
                "Checkpointed active time differs from independent Stopwatch by more than 2 seconds.");
            var completedPolls = polls;
            Require(activeLoops == 0, "Successful preparation left monitor running.");
            await Task.Delay(250, deadline.Token);
            Require(polls == completedPolls, "Stopped monitor polled after successful exit preparation.");
            var reopened = new SteamWatchRuntimeService(new(storeDirectory()), new Catalog(fixtureDirectory, Path.GetDirectoryName(gamePath)!),
                new WindowsProcessSnapshotProvider(), new WindowsRuntimeClock(), new Startup(), new NoTermination(), new Notifications());
            await reopened.InitializeAsync();
            reopenedTicks = await TicksAsync(new(storeDirectory()));
            Require(reopenedTicks == finalTicks, "Reopen changed or lost persisted ticks.");
            steps.Add("retry-stopped-monitor-and-reopen-preserved-exact-ticks");
            passed = true;
        }
        catch (Exception ex) { failure = ex.ToString(); throw; }
        finally
        {
            await monitor.StopAsync();
            // Disposable children have a fixed lifetime and exit naturally; never kill user processes.
            await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "active-exit-evidence.json"), JsonSerializer.Serialize(new
            {
                passed, failure, wu = "WU-RUNTIME-202", at = DateTimeOffset.UtcNow,
                kind = "service-lifecycle-with-controlled-Windows-process-not-native-UI-or-real-game",
                nominalPollSeconds = 10, seedSeconds = 59, expectedSeconds, measuredSeconds, toleranceSeconds = 2,
                reopenedTicks, monitorStarts = loops, successfulPolls = polls, failedPolls = failures, overlappingLoops = overlap,
                steps, samples, notifications = notifications.Messages.ToArray(),
                probeAssemblySha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(typeof(ActiveExitRecoveryProbe).Assembly.Location))),
                uncovered = new[] { "native UI/draft interaction", "real Steam game", "system notification rendering", "system sleep/overnight", "destructive force-close" }
            }, Json));
        }
        Console.WriteLine($"PASS active exit recovery: measured={measuredSeconds:F3}s expected={expectedSeconds:F3}s, polls={polls}, reminders={notifications.Messages.Count}");

        string storeDirectory() => Path.Combine(outputDirectory, "data");
        Task<bool> PrepareAsync() => exit.TryPrepareAsync(() => Task.FromResult(true), () => Task.FromResult(true),
            () => monitor.StopAndFlushAsync(async () => await runtime.FlushAsync()), monitor.Start);
    }

    private static async Task UntilAsync(Func<bool> predicate, CancellationToken token)
    {
        while (!predicate()) await Task.Delay(50, token);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static async Task<long> TicksAsync(JsonFileStore store) =>
        (await new PlaytimeLedgerStore(store).LoadAsync()).Export().Days.Sum(day => day.MeasuredTicks.GetValueOrDefault(AppId));
    private sealed class Catalog(string steam, string install) : ISteamCatalogProvider
    {
        public Task<SteamCatalog> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SteamCatalog(steam, "probe", [new(AppId, "Disposable active recovery fixture", 0,
                InstallDirectory: install, HasPlaytimeObservation: false)]));
    }
    private sealed class Notifications : INotificationService
    {
        public ConcurrentQueue<NotificationMessage> Messages { get; } = new();
        public void Show(NotificationMessage message) => Messages.Enqueue(message);
    }
    private sealed class Startup : IStartupManager
    {
        public StartupState GetState() => new(false, null);
        public void Enable(string executablePath) => throw new InvalidOperationException("Probe cannot edit startup.");
        public void Disable() => throw new InvalidOperationException("Probe cannot edit startup.");
    }
    private sealed class NoTermination : IGameProcessTerminator
    {
        public GameProcessTerminationResult Terminate(int processId) => throw new InvalidOperationException("Probe cannot close processes.");
        public Task<GameProcessTerminationResult> TerminateAsync(int processId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Probe cannot close processes.");
    }
}

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
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

internal static class ResidencyProbe
{
    private const int AppId = 1_234_567_890;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task RunAsync(string fixtureDirectory, string outputDirectory, int seconds)
    {
        if (seconds is < 60 or > 3600) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (Directory.Exists(outputDirectory)) throw new IOException("Evidence directory must be new.");
        Directory.CreateDirectory(outputDirectory);
        var store = new JsonFileStore(Path.Combine(outputDirectory, "data"));
        var today = DateOnly.FromDateTime(DateTime.Now);
        var seedDays = Enumerable.Range(0, 1825).Select(day => new DailyPlaytimeDuration(today.AddDays(-day),
            Enumerable.Range(0, 20).ToDictionary(i => i == 0 ? 730 : 10000 + i,
                i => (long)(i == 0 ? 60 : i * 60) * TimeSpan.TicksPerSecond), [])).ToList();
        var ledger = new PlaytimeLedger(new(2, seedDays, [], new() { [730] = "Synthetic residency fixture" }, []));
        await new PlaytimeLedgerStore(store).SaveAsync(ledger);
        // A separate, explicit I/O workload, outside the active CPU sampling window.
        var saves = new List<double>();
        for (var i = 0; i < 15; i++)
        {
            var watch = Stopwatch.StartNew();
            await new PlaytimeLedgerStore(store).SaveAsync(ledger);
            if (i >= 5) saves.Add(watch.Elapsed.TotalMilliseconds);
        }
        var game = Path.Combine(fixtureDirectory, "steamapps", "common", "Fixture", "game.exe");
        var processProvider = new TimedProcesses();
        var runtime = new SteamWatchRuntimeService(store, new Catalog(fixtureDirectory, Path.GetDirectoryName(game)!),
            processProvider, new WindowsRuntimeClock(), new Startup(), new NoTermination(), new Notifications());
        await runtime.RefreshGamesAsync();
        var observation = Path.Combine(outputDirectory, "child.json");
        var start = new ProcessStartInfo(Path.Combine(fixtureDirectory, "steam.exe")) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in new[] { "launch-game", game, (seconds + 30).ToString(), observation }) start.ArgumentList.Add(arg);
        using var parent = Process.Start(start) ?? throw new IOException("Fixture did not start.");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(seconds + 60));
        var samples = new List<object>();
        var costs = new List<double>();
        bool passed = false;
        string? failure = null;
        double expectedSeconds = 0, measuredSeconds = 0, flushMs = 0;
        ProcessSample? baseline = null, final = null;
        try
        {
            while (!File.Exists(observation)) await Task.Delay(50, deadline.Token);
            await runtime.PollAsync();
            Require(runtime.Current.Games.Single().IsRunning, "Production WMI did not detect fixture.");
            processProvider.Milliseconds.Clear();
            baseline = Sample();
            var clock = Stopwatch.StartNew();
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            while (clock.Elapsed.TotalSeconds < seconds)
            {
                await timer.WaitForNextTickAsync(deadline.Token);
                Require(!parent.HasExited, "Controlled process ended during active sampling.");
                var poll = Stopwatch.StartNew();
                await runtime.PollAsync(deadline.Token);
                var duration = poll.Elapsed.TotalMilliseconds;
                costs.Add(duration);
                var sample = new { seconds = clock.Elapsed.TotalSeconds, pollMs = duration, processSample = Sample() };
                samples.Add(sample);
                await File.AppendAllTextAsync(Path.Combine(outputDirectory, "samples.jsonl"), JsonSerializer.Serialize(sample) + "\n", deadline.Token);
            }
            var flush = Stopwatch.StartNew();
            await runtime.FlushAsync(deadline.Token);
            flushMs = flush.Elapsed.TotalMilliseconds;
            expectedSeconds = clock.Elapsed.TotalSeconds;
            final = Sample();
            var saved = (await new PlaytimeLedgerStore(store).LoadAsync()).Export();
            var ticks = saved.Days.Sum(day => day.MeasuredTicks.GetValueOrDefault(AppId));
            measuredSeconds = ticks / (double)TimeSpan.TicksPerSecond;
            Require(Math.Abs(expectedSeconds - measuredSeconds) <= 2, "Active ticks differ from independent Stopwatch.");
            var savedDays = saved.Days.ToDictionary(day => day.Date);
            foreach (var day in seedDays)
                foreach (var entry in day.MeasuredTicks)
                    Require(savedDays[day.Date].MeasuredTicks.GetValueOrDefault(entry.Key) == entry.Value, "Historical ticks changed.");
            Require(saved.Days.Count == 1825 && saved.EstimatedEntries!.Count == 0, "Unexpected dates or estimates.");
            var reopened = new SteamWatchRuntimeService(new(Path.Combine(outputDirectory, "data")),
                new Catalog(fixtureDirectory, Path.GetDirectoryName(game)!), new WindowsProcessSnapshotProvider(),
                new WindowsRuntimeClock(), new Startup(), new NoTermination(), new Notifications());
            await reopened.InitializeAsync();
            Require(reopened.Current.Records.Sum(day => day.GetGameMinutes(AppId)) == (int)(ticks / TimeSpan.TicksPerMinute),
                "Reopened measured minutes differ.");
            passed = true;
        }
        catch (Exception ex) { failure = ex.ToString(); throw; }
        finally
        {
            await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(seconds + 40));
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "residency.json"), JsonSerializer.Serialize(new
            {
                passed, failure, kind = "service-active-residency-with-controlled-Windows-process",
                at = DateTimeOffset.UtcNow, requestedSeconds = seconds, historyDays = 1825, gamesPerDay = 20,
                framework = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
                logicalProcessors = Environment.ProcessorCount, expectedSeconds, measuredSeconds, baseline, final,
                cpuPercentOfMachine = baseline is null || final is null || expectedSeconds == 0 ? (double?)null
                    : 100 * (final.CpuMs - baseline.CpuMs) / (expectedSeconds * 1000 * Environment.ProcessorCount),
                samples, polls = Summary(costs), wmi = Summary(processProvider.Milliseconds),
                isolatedAtomicSaves = Summary(saves), finalFlushMs = flushMs,
                ledgerBytes = new FileInfo(store.GetPath("playtime-ledger")).Length,
                probeSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(typeof(ResidencyProbe).Assembly.Location))),
                limits = new[] { "service process, excludes WinUI and tray rendering", "controlled child, not a Steam game",
                    "poll includes WMI, checkpoint, save, evaluation and publication; cannot subtract to claim pure save cost",
                    "standalone saves: 5 warmups and 10 samples before active window, OS cache not cleared",
                    "single machine/run and bounded duration, not overnight/sleep stability", "notification sink and no destructive rules" }
            }, Json));
        }
        Console.WriteLine($"PASS active residency: {costs.Count} polls, {expectedSeconds:F3}s observed / {measuredSeconds:F3}s measured.");
    }

    private static object Summary(List<double> values)
    {
        var ordered = values.Order().ToArray();
        return new { count = ordered.Length, medianMs = ordered.Length == 0 ? (double?)null : ordered[ordered.Length / 2],
            p95Ms = ordered.Length == 0 ? (double?)null : ordered[(int)Math.Ceiling(ordered.Length * .95) - 1], samplesMs = values };
    }
    private static ProcessSample Sample()
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        return new(process.TotalProcessorTime.TotalMilliseconds, process.WorkingSet64, process.PrivateMemorySize64,
            process.HandleCount, GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
    }
    private sealed record ProcessSample(double CpuMs, long WorkingSetBytes, long PrivateBytes, int Handles, int Gen0, int Gen1, int Gen2);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private sealed class TimedProcesses : IProcessSnapshotProvider
    {
        private readonly WindowsProcessSnapshotProvider _inner = new();
        public List<double> Milliseconds { get; } = [];
        public IReadOnlyList<SteamProcessSnapshot> GetProcesses()
        {
            var watch = Stopwatch.StartNew();
            try { return _inner.GetProcesses(); }
            finally { Milliseconds.Add(watch.Elapsed.TotalMilliseconds); }
        }
    }
    private sealed class Catalog(string steam, string install) : ISteamCatalogProvider
    {
        public Task<SteamCatalog> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SteamCatalog(steam, "probe", [new(AppId, "Disposable residency fixture", 0,
                InstallDirectory: install, HasPlaytimeObservation: false)]));
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
    private sealed class Notifications : INotificationService { public void Show(NotificationMessage message) { } }
}

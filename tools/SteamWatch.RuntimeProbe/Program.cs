using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SteamWatch.Core.Notifications;
using SteamWatch.Core.Settings;
using SteamWatch.Core.Steam;
using SteamWatch.Infrastructure.Enforcement;
using SteamWatch.Infrastructure.Notifications;
using SteamWatch.Infrastructure.Runtime;
using SteamWatch.Infrastructure.Startup;
using SteamWatch.Infrastructure.Steam;
using SteamWatch.Infrastructure.Storage;

namespace SteamWatch.RuntimeProbe;

internal static class Program
{
    private const int AppId = 1_234_567_890;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0) throw new ArgumentException("Use child, launch-game, timing, active-exit-recovery, residency, performance, data-performance, presentation-performance, upgrade-client or upgrade-disconnect.");
        switch (args[0])
        {
            case "residency":
                await ResidencyProbe.RunAsync(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]), int.Parse(args[3]));
                return 0;
            case "active-exit-recovery":
                await ActiveExitRecoveryProbe.RunAsync(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
                return 0;
            case "presentation-performance":
                PresentationPerformanceProbe.Run(Path.GetFullPath(args[1]));
                return 0;
            case "data-performance":
                await DataPerformanceProbe.RunAsync(Path.GetFullPath(args[1]));
                return 0;
            case "performance":
                PerformanceProbe.Run(Path.GetFullPath(args[1]));
                return 0;
            case "child":
                var duration = int.Parse(args[1]);
                var stopwatch = Stopwatch.StartNew();
                await File.WriteAllTextAsync(args[2], JsonSerializer.Serialize(new { startedAt = DateTimeOffset.UtcNow }, Json));
                await Task.Delay(TimeSpan.FromSeconds(duration));
                await File.WriteAllTextAsync(args[2], JsonSerializer.Serialize(new ChildResult(stopwatch.Elapsed.TotalSeconds), Json));
                return 0;
            case "launch-game":
                using (var child = Start(args[1], "child", args[2], args[3]))
                    await child.WaitForExitAsync();
                return 0;
            case "upgrade-client":
                var result = await UpgradeExitClient.RequestAsync(args[1], TimeSpan.FromSeconds(95));
                Console.WriteLine(result);
                return result == UpgradeExitResult.Prepared ? 0 : 2;
            case "upgrade-disconnect":
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(args[1]).ToUpperInvariant())));
                using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45)))
                {
                    await using (var pipe = new NamedPipeClientStream(".", "SteamWatch-Upgrade-" + key,
                        PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
                    {
                        await pipe.ConnectAsync(3000, deadline.Token);
                        await pipe.WriteAsync(new byte[] { 1 }, deadline.Token);
                        await pipe.FlushAsync(deadline.Token);
                        await File.WriteAllTextAsync(args[2] + ".ready", "request-sent", deadline.Token);
                        while (!File.Exists(args[2] + ".release")) await Task.Delay(50, deadline.Token);
                    }
                    await File.WriteAllTextAsync(args[2] + ".closed", "disconnected-without-response", deadline.Token);
                }
                return 0;
            case "timing":
                await RunTimingAsync(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
                return 0;
            default: throw new ArgumentException("Unknown probe mode.");
        }
    }

    private static async Task RunTimingAsync(string fixtureDirectory, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var gamePath = Path.Combine(fixtureDirectory, "steamapps", "common", "Fixture", "game.exe");
        var store = new JsonFileStore(Path.Combine(outputDirectory, "data"));
        var clock = new WindowsRuntimeClock();
        SteamWatchRuntimeService CreateRuntime() => new(store, new Catalog(fixtureDirectory, Path.GetDirectoryName(gamePath)!),
            new WindowsProcessSnapshotProvider(), clock, new Startup(), new NoTermination(), new Notifications());
        var runtime = CreateRuntime();
        await runtime.RefreshGamesAsync();
        var results = new List<object>();
        for (var index = 0; index < 3; index++)
        {
            var before = await MeasuredSecondsAsync(store);
            var observation = Path.Combine(outputDirectory, $"child-{index}.json");
            using var parent = Start(Path.Combine(fixtureDirectory, "steam.exe"), "launch-game", gamePath, "20", observation);
            var startDeadline = Stopwatch.StartNew();
            while (!File.Exists(observation))
            {
                if (parent.HasExited || startDeadline.Elapsed > TimeSpan.FromSeconds(10)) throw new InvalidOperationException("Fixture failed to start.");
                await Task.Delay(50);
            }
            await runtime.PollAsync();
            if (!runtime.Current.Games.Single().IsRunning) throw new InvalidOperationException("Production provider did not detect fixture.");
            var largestGap = 0d;
            var previousSample = Stopwatch.GetTimestamp();
            var refreshed = false;
            while (!parent.HasExited)
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                if (index == 1 && !refreshed)
                {
                    await runtime.RefreshGamesAsync();
                    refreshed = true;
                }
                if (index == 2 && !refreshed)
                {
                    await runtime.FlushAsync();
                    runtime = CreateRuntime();
                    await runtime.RefreshGamesAsync();
                    refreshed = true;
                }
                await runtime.PollAsync();
                largestGap = Math.Max(largestGap, Stopwatch.GetElapsedTime(previousSample).TotalSeconds);
                previousSample = Stopwatch.GetTimestamp();
            }
            await runtime.PollAsync();
            await runtime.FlushAsync();
            var expected = JsonSerializer.Deserialize<ChildResult>(await File.ReadAllTextAsync(observation), Json)!.ElapsedSeconds;
            var measured = await MeasuredSecondsAsync(store) - before;
            var error = measured - expected;
            var tolerance = largestGap + 2; // One observed polling interval plus startup scheduling allowance.
            if (Math.Abs(error) > tolerance) throw new InvalidOperationException($"Timing error {error:F3}s exceeds observed boundary {tolerance:F3}s.");
            results.Add(new { scenario = index switch { 0 => "short-session", 1 => "refresh-during-session", _ => "flush-restart-during-session" },
                expectedSeconds = expected, measuredSeconds = measured, errorSeconds = error,
                largestObservedPollGapSeconds = largestGap, toleranceSeconds = tolerance });
            Console.WriteLine($"PASS timing case {index + 1}: expected={expected:F3}s measured={measured:F3}s error={error:F3}s");
        }
        var termination = await VerifyTerminationAsync(gamePath, outputDirectory);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "runtime-evidence.json"), JsonSerializer.Serialize(new
        {
            kind = "controlled-Windows-process-not-real-Steam-game", at = DateTimeOffset.UtcNow,
            clock = "WindowsRuntimeClock", processProvider = "WindowsProcessSnapshotProvider", nominalPollSeconds = 10,
            cases = results, termination, totalMeasuredSeconds = await MeasuredSecondsAsync(store),
            uncovered = new[] { "actual Steam game", "overnight/week boundary", "system sleep/clock changes", "full-screen notifications", "permissions/anti-cheat" }
        }, Json));
    }

    private static async Task<object> VerifyTerminationAsync(string gamePath, string outputDirectory)
    {
        using var fixture = Start(gamePath, "child", "60", Path.Combine(outputDirectory, "termination-child.json"));
        var terminator = new GameProcessTerminator();
        var sample = new RunningSteamGame(AppId, fixture.Id, "game.exe", "Disposable fixture", gamePath, fixture.StartTime.ToUniversalTime().Ticks);
        var mismatched = await terminator.TerminateAsync(sample with { StartTimeUtcTicks = sample.StartTimeUtcTicks + 1 });
        if (mismatched.IsSuccess || fixture.HasExited) throw new InvalidOperationException("Identity mismatch did not prevent termination.");
        var result = await terminator.TerminateAsync(sample);
        await fixture.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        if (!result.IsSuccess || !fixture.HasExited) throw new InvalidOperationException("Fixture exit was not confirmed.");
        Console.WriteLine("PASS controlled process termination: mismatched identity refused, matching exit confirmed.");
        return new { identityMismatchRefused = true, actualExitConfirmed = true };
    }

    private static async Task<double> MeasuredSecondsAsync(JsonFileStore store) =>
        (await new PlaytimeLedgerStore(store).LoadAsync()).Export().Days.Sum(day => day.MeasuredTicks.GetValueOrDefault(AppId)) / (double)TimeSpan.TicksPerSecond;

    private static Process Start(string executable, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return Process.Start(start) ?? throw new InvalidOperationException("Fixture process did not start.");
    }
    private sealed record ChildResult(double ElapsedSeconds);
    private sealed class Catalog(string steam, string install) : ISteamCatalogProvider
    {
        public Task<SteamCatalog> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SteamCatalog(steam, "probe", [new(AppId, "Disposable fixture", 0, InstallDirectory: install, HasPlaytimeObservation: false)]));
    }
    private sealed class Startup : IStartupManager
    {
        public StartupState GetState() => new(false, null);
        public void Enable(string executablePath) => throw new InvalidOperationException("Probe cannot edit startup settings.");
        public void Disable() => throw new InvalidOperationException("Probe cannot edit startup settings.");
    }
    private sealed class NoTermination : IGameProcessTerminator
    {
        public GameProcessTerminationResult Terminate(int processId) => throw new InvalidOperationException("Timing probe cannot close processes.");
        public Task<GameProcessTerminationResult> TerminateAsync(int processId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Timing probe cannot close processes.");
    }
    private sealed class Notifications : INotificationService { public void Show(NotificationMessage message) { } }
}

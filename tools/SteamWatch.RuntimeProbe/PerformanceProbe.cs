using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using SteamWatch.Core.Limits;
using SteamWatch.Core.Tracking;

namespace SteamWatch.RuntimeProbe;

/// <summary>Deterministic in-memory workload; excludes process sampling, disk and WinUI.</summary>
internal static class PerformanceProbe
{
    public static void Run(string outputPath)
    {
        var today = new DateOnly(2026, 10, 14);
        const int days = 1825, games = 500, gamesPerDay = 20, iterations = 100, warmup = 20;
        var records = Enumerable.Range(0, days).Select(day => new DailyPlaytimeRecord(today.AddDays(3 - day),
            Enumerable.Range(0, gamesPerDay).ToDictionary(i => (day * 17 + i) % games + 1, i => i + 1))).ToArray();
        var book = new PlaytimeRecordBook(records);
        var rules = Enumerable.Range(1, games).SelectMany(id => new[] { LimitPeriod.Day, LimitPeriod.Week }
            .Select(period => new LimitRule(LimitScope.Game, period, 120, EnforcementMode.NotifyOnly, id)))
            .Concat(new[] { LimitPeriod.Day, LimitPeriod.Week }.Select(period =>
                new LimitRule(LimitScope.Global, period, 600, EnforcementMode.NotifyOnly))).ToArray();
        var coordinator = new RuntimeLimitCoordinator();
        var running = new HashSet<int> { 1, 2 };
        var now = new DateTimeOffset(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
        var limits = Measure(() => coordinator.Evaluate(rules, records, today, running, now)
            .Decisions.Sum(d => (long)d.Evaluation.UsedMinutes));
        var projection = Measure(() => Enumerable.Range(1, games)
            .Sum(id => (long)book.GetGameMinutes(today, id) + book.GetWeekGameMinutes(today, id))
            + book.GetWeekTotalMinutes(today));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(new
        {
            scope = "Synthetic in-memory rules and weekly projection; not startup, UI, disk or real-game acceptance",
            framework = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            days, games, gamesPerDay, rules = rules.Length, iterations, warmup, limits, projection
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        Console.WriteLine(outputPath);

        object Measure(Func<long> operation)
        {
            for (var i = 0; i < warmup; i++) operation();
            var samples = new double[iterations];
            long bytes = 0, checksum = 0;
            for (var i = 0; i < iterations; i++)
            {
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                var start = Stopwatch.GetTimestamp();
                checksum = checked(checksum + operation());
                samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                bytes += GC.GetAllocatedBytesForCurrentThread() - allocated;
            }
            Array.Sort(samples);
            return new { medianMs = samples[iterations / 2], p95Ms = samples[(int)Math.Ceiling(iterations * .95) - 1],
                allocatedBytesPerIteration = bytes / iterations, checksum };
        }
    }
}

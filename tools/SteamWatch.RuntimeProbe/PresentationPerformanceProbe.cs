using System.Diagnostics;
using System.Text.Json;
using SteamWatch.App.Presentation;
using SteamWatch.Core.Tracking;
using SteamWatch.Infrastructure.Runtime;

namespace SteamWatch.RuntimeProbe;

internal static class PresentationPerformanceProbe
{
    public static void Run(string outputPath)
    {
        var today = new DateOnly(2026, 10, 14);
        const int days = 1825, games = 500, gamesPerDay = 20, warmup = 20, iterations = 100;
        var records = Enumerable.Range(0, days).Select(day => new DailyPlaytimeRecord(today.AddDays(-day),
            Enumerable.Range(0, gamesPerDay).ToDictionary(i => (day * 17 + i) % games + 1, i => i + 1))).ToArray();
        var snapshot = RuntimeSnapshot.Empty with { Records = records };
        long Project()
        {
            var model = new RuntimeReadModel(snapshot, today);
            return model.DailyStats(today).Sum(row => (long)row.TotalMinutes)
                + model.WeeklyStats(today).Sum(row => (long)row.TotalMinutes)
                + model.WeekStatistics(today).Sum(day => (long)day.TotalMinutes)
                + (long)model.Budgets.Day.Progress + (long)model.Budgets.Week.Progress;
        }
        for (var i = 0; i < warmup; i++) Project();
        var samples = new double[iterations];
        long bytes = 0, checksum = 0;
        for (var i = 0; i < iterations; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            checksum += Project();
            samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            bytes += GC.GetAllocatedBytesForCurrentThread() - before;
        }
        Array.Sort(samples);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(new
        {
            scope = "Synthetic current UI read-model construction and day/week/chart projection, excludes XAML rendering",
            days, games, gamesPerDay, warmup, iterations, checksum,
            medianMs = samples[iterations / 2], p95Ms = samples[(int)Math.Ceiling(iterations * .95) - 1],
            allocatedBytesPerIteration = bytes / iterations
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        Console.WriteLine(outputPath);
    }
}

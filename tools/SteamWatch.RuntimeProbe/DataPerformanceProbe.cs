using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using SteamWatch.Core.Tracking;
using SteamWatch.Infrastructure.Storage;

namespace SteamWatch.RuntimeProbe;

internal static class DataPerformanceProbe
{
    public static async Task RunAsync(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var today = new DateOnly(2026, 10, 14);
        const int days = 1825, games = 500, gamesPerDay = 20;
        var names = Enumerable.Range(1, games).ToDictionary(id => id, id => $"Fixture {id}");
        var ledger = new PlaytimeLedger(new(PlaytimeLedger.CurrentSchemaVersion,
            Enumerable.Range(0, days).Select(day => new DailyPlaytimeDuration(today.AddDays(-day),
                Enumerable.Range(0, gamesPerDay).ToDictionary(i => (day * 17 + i) % games + 1,
                    i => (long)(i + 1) * TimeSpan.TicksPerMinute), new())).ToList(),
            new() { ["synthetic-account"] = names.Keys.ToDictionary(id => id, id => (decimal)id) }, names, []));
        var store = new PlaytimeLedgerStore(new JsonFileStore(Path.Combine(outputDirectory, "data")));
        var projection = await MeasureAsync(100, () =>
        {
            var snapshot = ledger.CreateReadSnapshot();
            return Task.FromResult(snapshot.Records.Sum(d => (long)d.TotalMinutes) + snapshot.GameNames.Count
                + snapshot.EstimatedRecords.Count + snapshot.EstimatedEntries.Count);
        });
        var saving = await MeasureAsync(20, async () =>
        {
            await store.SaveAsync(ledger);
            return new FileInfo(Path.Combine(outputDirectory, "data/playtime-ledger.json")).Length;
        });
        var loaded = await store.LoadAsync();
        var roundtripMinutes = loaded.GetRecords().Sum(d => (long)d.TotalMinutes);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "performance.json"), JsonSerializer.Serialize(new
        {
            scope = "Synthetic ledger display projection and local atomic JSON saving; excludes WinUI and process sampling",
            framework = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
            days, games, gamesPerDay, warmup = 5, projection, saving, roundtripMinutes
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        Console.WriteLine(outputDirectory);
    }

    private static async Task<object> MeasureAsync(int iterations, Func<Task<long>> operation)
    {
        for (var i = 0; i < 5; i++) await operation();
        var samples = new double[iterations];
        long allocated = 0, checksum = 0;
        for (var i = 0; i < iterations; i++)
        {
            var before = GC.GetTotalAllocatedBytes(precise: true);
            var start = Stopwatch.GetTimestamp();
            checksum += await operation();
            samples[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            allocated += GC.GetTotalAllocatedBytes(precise: true) - before;
        }
        Array.Sort(samples);
        return new { iterations, medianMs = samples[iterations / 2],
            p95Ms = samples[(int)Math.Ceiling(iterations * .95) - 1],
            processAllocatedBytesPerIteration = allocated / iterations, checksum };
    }
}

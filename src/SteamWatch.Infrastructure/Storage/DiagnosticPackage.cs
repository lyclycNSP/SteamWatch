using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using SteamWatch.Infrastructure.Runtime;

namespace SteamWatch.Infrastructure.Storage;

/// <summary>Export an explicit allowlist. Never serialize settings, paths, names or backup data.</summary>
public static class DiagnosticPackage
{
    private const int MaxLogBytes = 256 * 1024;
    private static readonly HashSet<string> Categories = ["runtime", "monitor", "notification", "startup", "storage", "upgrade"];
    private static readonly HashSet<string> ErrorTypes = ["IOException", "UnauthorizedAccessException", "InvalidDataException",
        "JsonException", "InvalidOperationException", "ArgumentException", "TimeoutException", "Win32Exception", "COMException", "ManagementException"];

    public static byte[] Create(string dataDirectory, RuntimeSnapshot snapshot)
    {
        var logs = ReadSanitizedLog(Path.Combine(dataDirectory, "diagnostics.log"));
        var report = new
        {
            schemaVersion = 1, exportedAt = DateTimeOffset.UtcNow,
            version = typeof(DiagnosticPackage).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            os = Environment.OSVersion.Version.ToString(), architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            monitoringPaused = snapshot.IsPaused, installedItemCount = snapshot.Games.Count,
            runningItemCount = snapshot.Games.Count(g => g.IsRunning), ruleCount = snapshot.Rules.Count,
            recordedDayCount = snapshot.Records.Count, estimatedEntryCount = snapshot.EstimatedEntries.Count,
            logStatus = logs.Status, privacy = "Only counts and allowlisted error types are included; no settings, credentials, accounts, game names or paths."
        };
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteJson(archive, "report.json", report);
            WriteJson(archive, "diagnostics.json", logs.Entries);
        }
        if (output.Length > 1024 * 1024) throw new InvalidDataException("诊断包超过 1 MiB 限制。");
        return output.ToArray();
    }

    private static (string Status, List<object> Entries) ReadSanitizedLog(string path)
    {
        var entries = new List<object>();
        if (!File.Exists(path)) return ("no-log", entries);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var length = stream.Length;
            var start = Math.Max(0, length - MaxLogBytes);
            stream.Seek(start, SeekOrigin.Begin);
            var bytes = new byte[(int)(length - start)];
            stream.ReadExactly(bytes);
            using var reader = new StringReader(Encoding.UTF8.GetString(bytes));
            if (start > 0) reader.ReadLine();
            while (reader.ReadLine() is { } line)
            {
                try
                {
                    using var json = JsonDocument.Parse(line);
                    var root = json.RootElement;
                    if (root.ValueKind != JsonValueKind.Object
                        || !root.TryGetProperty("at", out var at) || at.ValueKind != JsonValueKind.String || !at.TryGetDateTimeOffset(out var time)
                        || !root.TryGetProperty("category", out var category) || category.ValueKind != JsonValueKind.String
                        || !Categories.Contains(category.GetString()!)
                        || !root.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.String) continue;
                    entries.Add(new { at = time, category = category.GetString(),
                        error = ErrorTypes.Contains(error.GetString()!) ? error.GetString() : "OtherException" });
                    if (entries.Count > 1000) entries.RemoveAt(0);
                }
                catch (JsonException) { /* Incomplete or foreign lines are not exported. */ }
            }
            return (start > 0 ? "last-256-KiB" : "available", entries);
        }
        catch (IOException) { return ("unreadable", entries); }
        catch (UnauthorizedAccessException) { return ("unreadable", entries); }
    }

    private static void WriteJson<T>(ZipArchive archive, string name, T value)
    {
        using var stream = archive.CreateEntry(name, CompressionLevel.Optimal).Open();
        JsonSerializer.Serialize(stream, value, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true });
    }
}

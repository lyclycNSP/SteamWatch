using System.Text.Json;
using System.Collections.Concurrent;

namespace SteamWatch.Infrastructure.Storage;

public sealed class JsonFileStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    // Coordinate independent store instances targeting the same file in this process.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _dataDirectory;
    public string? RecoveryWarning { get; private set; }

    public JsonFileStore(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            throw new ArgumentException("Data directory is required.", nameof(dataDirectory));
        }

        _dataDirectory = Path.GetFullPath(dataDirectory);
        Directory.CreateDirectory(_dataDirectory);
    }

    public async Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        var path = GetPath(key);
        var gate = Gates.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 65536, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path)) File.Replace(tempPath, path, $"{path}.bak");
            else File.Move(tempPath, path);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            gate.Release();
        }
    }

    public async Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var path = GetPath(key);
        var gate = Gates.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path)) return default;
            try { return await ReadAsync<T>(path, cancellationToken).ConfigureAwait(false); }
            catch (JsonException ex)
            {
                if (!File.Exists($"{path}.bak"))
                    throw new InvalidDataException($"数据文件 {key} 已损坏；原文件已保留，请恢复备份。", ex);
                var recovered = await ReadAsync<T>($"{path}.bak", cancellationToken).ConfigureAwait(false);
                File.Move(path, $"{path}.corrupt-{Guid.NewGuid():N}");
                File.Copy($"{path}.bak", path);
                RecoveryWarning = $"{key} 已从上一份备份恢复；损坏文件已保留。";
                return recovered;
            }
        }
        finally { gate.Release(); }
    }

    private static async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.Read | FileShare.Delete, 4096, FileOptions.Asynchronous);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken).ConfigureAwait(false);
        return value is null ? throw new JsonException("Stored data must not be null.") : value;
    }

    public bool Exists(string key)
    {
        return File.Exists(GetPath(key));
    }

    public void Delete(string key)
    {
        var path = GetPath(key);
        var gate = Gates.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        gate.Wait();
        try { if (File.Exists(path)) File.Delete(path); }
        finally { gate.Release(); }
    }

    public string GetPath(string key)
    {
        ValidateKey(key);
        return Path.Combine(_dataDirectory, $"{key}.json");
    }

    private static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Storage key is required.", nameof(key));
        }

        if (key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Storage key contains invalid file name characters.", nameof(key));
        }
    }
}

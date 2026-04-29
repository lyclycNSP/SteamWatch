using System.Text.Json;

namespace SteamWatch.Infrastructure.Storage;

public sealed class JsonFileStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _dataDirectory;

    public JsonFileStore(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            throw new ArgumentException("Data directory is required.", nameof(dataDirectory));
        }

        _dataDirectory = dataDirectory;
        Directory.CreateDirectory(_dataDirectory);
    }

    public async Task SaveAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        var path = GetPath(key);
        var tempPath = $"{path}.tmp";
        var json = JsonSerializer.Serialize(value, Options);

        await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, null);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }

    public async Task<T?> LoadAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var path = GetPath(key);
        if (!File.Exists(path))
        {
            return default;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken).ConfigureAwait(false);
    }

    public bool Exists(string key)
    {
        return File.Exists(GetPath(key));
    }

    public void Delete(string key)
    {
        var path = GetPath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
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

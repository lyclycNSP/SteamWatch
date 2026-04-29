using SteamWatch.Core.Steam;

namespace SteamWatch.Infrastructure.Steam;

public sealed class SteamCacheReader
{
    private readonly SteamManifestParser _parser;

    public SteamCacheReader(string steamPath, SteamManifestParser? parser = null)
    {
        if (string.IsNullOrWhiteSpace(steamPath))
        {
            throw new ArgumentException("Steam path is required.", nameof(steamPath));
        }

        SteamPath = steamPath;
        _parser = parser ?? new SteamManifestParser();
    }

    public string SteamPath { get; }

    public async Task<IReadOnlyList<SteamGameInfo>> ReadGamesAsync(
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedUserId = userId ?? await GetCurrentUserIdAsync(cancellationToken).ConfigureAwait(false);
        var playtimes = resolvedUserId is null
            ? new Dictionary<int, int>()
            : await ReadPlaytimesAsync(resolvedUserId, cancellationToken).ConfigureAwait(false);

        var steamAppsPath = Path.Combine(SteamPath, "steamapps");
        if (!Directory.Exists(steamAppsPath))
        {
            return [];
        }

        var games = new List<SteamGameInfo>();
        foreach (var manifestPath in Directory.EnumerateFiles(steamAppsPath, "appmanifest_*.acf"))
        {
            var content = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
            var manifest = _parser.ParseAppManifest(content);
            if (manifest is null)
            {
                continue;
            }

            games.Add(new SteamGameInfo(
                manifest.AppId,
                manifest.Name,
                playtimes.GetValueOrDefault(manifest.AppId),
                GetGameIconPath(manifest.AppId)));
        }

        return games.OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<string?> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
    {
        var loginUsersPath = Path.Combine(SteamPath, "config", "loginusers.vdf");
        if (File.Exists(loginUsersPath))
        {
            var content = await File.ReadAllTextAsync(loginUsersPath, cancellationToken).ConfigureAwait(false);
            var mostRecent = _parser.ParseMostRecentUserId32(content);
            if (!string.IsNullOrWhiteSpace(mostRecent))
            {
                return mostRecent;
            }
        }

        var userdataPath = Path.Combine(SteamPath, "userdata");
        if (!Directory.Exists(userdataPath))
        {
            return null;
        }

        return Directory.EnumerateDirectories(userdataPath)
            .Select(Path.GetFileName)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
    }

    public string? GetUserDataPath(string userId)
    {
        var path = Path.Combine(SteamPath, "userdata", userId);
        return Directory.Exists(path) ? path : null;
    }

    public string? GetGameIconPath(int appId)
    {
        var libraryCache = Path.Combine(SteamPath, "appcache", "librarycache");
        if (!Directory.Exists(libraryCache))
        {
            return null;
        }

        foreach (var fileName in new[] { $"{appId}_icon.jpg", $"{appId}_icon.png", $"{appId}_header.jpg" })
        {
            var path = Path.Combine(libraryCache, fileName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private async Task<IReadOnlyDictionary<int, int>> ReadPlaytimesAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var localConfigPath = Path.Combine(SteamPath, "userdata", userId, "config", "localconfig.vdf");
        if (!File.Exists(localConfigPath))
        {
            return new Dictionary<int, int>();
        }

        var content = await File.ReadAllTextAsync(localConfigPath, cancellationToken).ConfigureAwait(false);
        return _parser.ParsePlaytimes(content);
    }
}

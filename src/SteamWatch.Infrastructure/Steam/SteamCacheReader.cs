using SteamWatch.Core.Steam;

namespace SteamWatch.Infrastructure.Steam;

public sealed class SteamCacheReader
{
    private readonly SteamManifestParser _parser;
    private IReadOnlyDictionary<int, string>? _clientIconHashes;

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

        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(SteamPath) };
        var foldersFile = Path.Combine(SteamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(foldersFile))
        {
            var folders = await File.ReadAllTextAsync(foldersFile, cancellationToken).ConfigureAwait(false);
            foreach (var library in _parser.ParseLibraryPaths(folders)) libraries.Add(Path.GetFullPath(library));
        }
        var games = new Dictionary<int, SteamGameInfo>();
        foreach (var library in libraries)
        {
            var steamAppsPath = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamAppsPath)) continue;
            foreach (var manifestPath in Directory.EnumerateFiles(steamAppsPath, "appmanifest_*.acf"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var content = await File.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
                var manifest = _parser.ParseAppManifest(content);
                if (manifest is null) continue;
                var folder = manifest.InstallDirectory ?? manifest.Name;
                var commonRoot = Path.GetFullPath(Path.Combine(steamAppsPath, "common")) + Path.DirectorySeparatorChar;
                var installPath = Path.GetFullPath(Path.Combine(commonRoot, folder));
                if (!installPath.StartsWith(commonRoot, StringComparison.OrdinalIgnoreCase)) continue;
                games[manifest.AppId] = new SteamGameInfo(manifest.AppId, manifest.Name,
                    playtimes.GetValueOrDefault(manifest.AppId), GetGameIconPath(manifest.AppId), installPath, playtimes.ContainsKey(manifest.AppId));
            }
        }
        return games.Values.OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
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
        var appInfoIconPath = GetAppInfoIconPath(appId);
        if (appInfoIconPath is not null)
        {
            return appInfoIconPath;
        }

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

        var appCacheDirectory = Path.Combine(libraryCache, appId.ToString());
        return Directory.Exists(appCacheDirectory)
            ? FindBestLibraryCacheImage(appCacheDirectory)
            : null;
    }

    private string? GetAppInfoIconPath(int appId)
    {
        var iconHashes = _clientIconHashes ??= SteamAppInfoIconReader.ReadClientIconHashes(
            Path.Combine(SteamPath, "appcache", "appinfo.vdf"));
        if (!iconHashes.TryGetValue(appId, out var iconHash))
        {
            return null;
        }

        var iconPath = Path.Combine(SteamPath, "steam", "games", $"{iconHash}.ico");
        return File.Exists(iconPath) ? iconPath : null;
    }

    private static string? FindBestLibraryCacheImage(string appCacheDirectory)
    {
        var preferredNames = new[]
        {
            "library_capsule.jpg",
            "library_header.jpg",
            "library_header_schinese.jpg",
            "header.jpg",
            "header_schinese.jpg",
            "library_600x900.jpg",
            "library_600x900_schinese.jpg",
            "logo.png",
            "logo_schinese.png"
        };

        foreach (var fileName in preferredNames)
        {
            var directPath = Path.Combine(appCacheDirectory, fileName);
            if (File.Exists(directPath))
            {
                return directPath;
            }

            var nestedPath = Directory
                .EnumerateFiles(appCacheDirectory, fileName, SearchOption.AllDirectories)
                .FirstOrDefault();
            if (nestedPath is not null)
            {
                return nestedPath;
            }
        }

        return Directory
            .EnumerateFiles(appCacheDirectory, "*.png", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(appCacheDirectory, "*.jpg", SearchOption.AllDirectories))
            .FirstOrDefault();
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

namespace SteamWatch.Infrastructure.Steam;

public sealed record SteamManifestEntry(int AppId, string Name, string? InstallDirectory = null);

public sealed class SteamManifestParser
{
    public SteamManifestEntry? ParseAppManifest(string content)
    {
        var root = ValveKeyValues.Parse(content);
        var app = root.Find("AppState") ?? root;
        return int.TryParse(app.Get("appid"), out var id) && id > 0 && app.Get("name") is { Length: > 0 } name
            ? new(id, name, app.Get("installdir")) : null;
    }

    public IReadOnlyDictionary<int, int> ParsePlaytimes(string content)
    {
        var root = ValveKeyValues.Parse(content);
        var apps = root.Find("apps") ?? root;
        return apps.Children.Where(pair => int.TryParse(pair.Key, out var id) && id > 0
                && int.TryParse(pair.Value.Get("Playtime"), out var minutes) && minutes >= 0)
            .ToDictionary(pair => int.Parse(pair.Key), pair => int.Parse(pair.Value.Get("Playtime")!));
    }

    public string? ParseMostRecentUserId32(string content)
    {
        var root = ValveKeyValues.Parse(content);
        var users = root.Find("users") ?? root;
        var user = users.Children.FirstOrDefault(pair => pair.Value.Get("MostRecent") == "1");
        return user.Key is null ? null : SteamIdConverter.ToSteamId32(user.Key);
    }

    public IReadOnlyList<string> ParseLibraryPaths(string content)
    {
        var root = ValveKeyValues.Parse(content);
        var libraries = root.Find("libraryfolders") ?? root;
        return libraries.Children.Values.Select(node => node.Get("path"))
            .Concat(libraries.Values.Where(pair => int.TryParse(pair.Key, out _)).Select(pair => pair.Value))
            .OfType<string>().Where(Path.IsPathFullyQualified).ToList();
    }
}

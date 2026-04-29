using System.Text.RegularExpressions;

namespace SteamWatch.Infrastructure.Steam;

public sealed record SteamManifestEntry(int AppId, string Name);

public sealed class SteamManifestParser
{
    private static readonly Regex AppIdRegex = new("\"appid\"\\s*\"(?<value>\\d+)\"", RegexOptions.Compiled);
    private static readonly Regex NameRegex = new("\"name\"\\s*\"(?<value>[^\"]+)\"", RegexOptions.Compiled);

    public SteamManifestEntry? ParseAppManifest(string content)
    {
        var appIdMatch = AppIdRegex.Match(content);
        var nameMatch = NameRegex.Match(content);

        if (!appIdMatch.Success || !nameMatch.Success)
        {
            return null;
        }

        return new SteamManifestEntry(
            int.Parse(appIdMatch.Groups["value"].Value),
            nameMatch.Groups["value"].Value);
    }

    public IReadOnlyDictionary<int, int> ParsePlaytimes(string localConfigContent)
    {
        var result = new Dictionary<int, int>();
        var appBlocks = Regex.Matches(
            localConfigContent,
            "\"(?<appId>\\d+)\"\\s*\\{(?<body>[\\s\\S]*?)\\n\\s*\\}",
            RegexOptions.Compiled);

        foreach (Match appBlock in appBlocks)
        {
            var body = appBlock.Groups["body"].Value;
            var playtimeMatch = Regex.Match(body, "\"Playtime\"\\s*\"(?<value>\\d+)\"");
            if (!playtimeMatch.Success)
            {
                continue;
            }

            result[int.Parse(appBlock.Groups["appId"].Value)] = int.Parse(playtimeMatch.Groups["value"].Value);
        }

        return result;
    }

    public string? ParseMostRecentUserId32(string loginUsersContent)
    {
        var matches = Regex.Matches(
            loginUsersContent,
            "\"(?<steamId64>\\d{17})\"\\s*\\{(?<body>[\\s\\S]*?)\\n\\s*\\}",
            RegexOptions.Compiled);

        foreach (Match match in matches)
        {
            if (Regex.IsMatch(match.Groups["body"].Value, "\"MostRecent\"\\s*\"1\""))
            {
                return SteamIdConverter.ToSteamId32(match.Groups["steamId64"].Value);
            }
        }

        return null;
    }
}

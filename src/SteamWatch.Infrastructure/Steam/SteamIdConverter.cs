namespace SteamWatch.Infrastructure.Steam;

public static class SteamIdConverter
{
    private const long SteamId64Offset = 76561197960265728;

    public static string ToSteamId32(string steamId64)
    {
        if (!long.TryParse(steamId64, out var value))
        {
            throw new ArgumentException("Steam ID must be numeric.", nameof(steamId64));
        }

        return (value - SteamId64Offset).ToString();
    }
}

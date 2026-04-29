namespace SteamWatch.Core.Tracking;

public sealed record DailyPlaytimeRecord(DateOnly Date, IReadOnlyDictionary<int, int> GameMinutes)
{
    public int GetGameMinutes(int appId)
    {
        return GameMinutes.TryGetValue(appId, out var minutes) ? minutes : 0;
    }

    public int TotalMinutes => GameMinutes.Values.Sum();
}

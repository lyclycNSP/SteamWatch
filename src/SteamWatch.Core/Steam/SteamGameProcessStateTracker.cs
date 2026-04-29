namespace SteamWatch.Core.Steam;

public sealed class SteamGameProcessStateTracker
{
    private IReadOnlyDictionary<int, RunningSteamGame> _running = new Dictionary<int, RunningSteamGame>();

    public SteamGameProcessChangeSet Update(IReadOnlyList<RunningSteamGame> current)
    {
        var currentByAppId = current.ToDictionary(game => game.AppId);

        var started = currentByAppId
            .Where(pair => !_running.ContainsKey(pair.Key))
            .Select(pair => pair.Value)
            .OrderBy(game => game.AppId)
            .ToList();

        var stopped = _running
            .Where(pair => !currentByAppId.ContainsKey(pair.Key))
            .Select(pair => pair.Value)
            .OrderBy(game => game.AppId)
            .ToList();

        _running = currentByAppId;

        return new SteamGameProcessChangeSet(
            started,
            stopped,
            _running.Values.OrderBy(game => game.AppId).ToList());
    }
}

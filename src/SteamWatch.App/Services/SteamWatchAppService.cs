using SteamWatch.Core.Steam;
using SteamWatch.Core.Tracking;
using SteamWatch.Infrastructure.Steam;

namespace SteamWatch.App.Services;

public sealed class SteamWatchAppService
{
    private readonly SteamPathResolver _pathResolver;
    private readonly Dictionary<int, GameRowViewModel> _games = [];
    private readonly Dictionary<int, int> _sessionMinutesByAppId = [];
    private readonly Dictionary<int, RunningGameSession> _sessions = [];
    private SteamProcessMonitor? _monitor;
    private string? _steamPath;

    public SteamWatchAppService(SteamPathResolver? pathResolver = null)
    {
        _pathResolver = pathResolver ?? new SteamPathResolver();
    }

    public async Task<GameListSnapshot> LoadGameListAsync(CancellationToken cancellationToken = default)
    {
        var steamPath = _pathResolver.Resolve();
        _steamPath = steamPath;
        var reader = new SteamCacheReader(steamPath);
        var userId = await reader.GetCurrentUserIdAsync(cancellationToken).ConfigureAwait(false);
        var games = await reader.ReadGamesAsync(userId, cancellationToken).ConfigureAwait(false);

        var rows = games
            .Select(game => new GameRowViewModel(
                game.AppId,
                game.Name,
                game.PlaytimeForeverMinutes,
                "未运行",
                "今日 0 分钟",
                "本周 0 分钟"))
            .ToList();

        _games.Clear();
        foreach (var row in rows)
        {
            _games[row.AppId] = row;
        }

        _monitor = new SteamProcessMonitor(
            new WindowsProcessSnapshotProvider(),
            new SteamGameProcessIdentifier(games, steamPath));

        var status = userId is null
            ? $"已找到 Steam：{steamPath}，但未找到当前用户"
            : $"已找到 Steam：{steamPath}，用户 {userId}，游戏 {rows.Count} 个";

        return new GameListSnapshot(steamPath, userId, status, rows);
    }

    public GameStatusSnapshot PollRuntimeStatus(DateTimeOffset now)
    {
        if (_monitor is null || _games.Count == 0)
        {
            return new GameStatusSnapshot("监控尚未初始化，请先刷新游戏列表。", _games.Values.ToList());
        }

        var changes = _monitor.Poll();
        foreach (var started in changes.Started)
        {
            _sessions[started.AppId] = new RunningGameSession(
                started.AppId,
                started.ProcessId,
                ResolveGameName(started),
                now);
        }

        foreach (var running in changes.Running)
        {
            if (!_sessions.TryGetValue(running.AppId, out var session))
            {
                _sessions[running.AppId] = new RunningGameSession(
                    running.AppId,
                    running.ProcessId,
                    ResolveGameName(running),
                    now);
                continue;
            }

            var increment = session.Checkpoint(now);
            AddSessionMinutes(increment);
        }

        foreach (var stopped in changes.Stopped)
        {
            if (!_sessions.Remove(stopped.AppId, out var session))
            {
                continue;
            }

            var increment = session.Complete(now);
            AddSessionMinutes(increment);
        }

        var runningAppIds = changes.Running.Select(game => game.AppId).ToHashSet();
        var rows = _games.Values
            .OrderBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(game =>
            {
                var minutes = _sessionMinutesByAppId.GetValueOrDefault(game.AppId);
                return game.WithRuntimeState(runningAppIds.Contains(game.AppId), minutes, minutes);
            })
            .ToList();

        var status = runningAppIds.Count == 0
            ? $"监控中：未检测到运行中的 Steam 游戏。Steam 路径：{_steamPath}"
            : $"监控中：{runningAppIds.Count} 个 Steam 游戏正在运行。";

        return new GameStatusSnapshot(status, rows);
    }

    private string ResolveGameName(RunningSteamGame game)
    {
        if (_games.TryGetValue(game.AppId, out var row))
        {
            return row.Name;
        }

        return game.GameName ?? game.ProcessName;
    }

    private void AddSessionMinutes(PlaytimeIncrement increment)
    {
        if (increment.Minutes <= 0)
        {
            return;
        }

        _sessionMinutesByAppId[increment.AppId] =
            _sessionMinutesByAppId.GetValueOrDefault(increment.AppId) + increment.Minutes;
    }
}

using SteamWatch.Infrastructure.Steam;

namespace SteamWatch.App.Services;

public sealed class SteamWatchAppService
{
    private readonly SteamPathResolver _pathResolver;

    public SteamWatchAppService(SteamPathResolver? pathResolver = null)
    {
        _pathResolver = pathResolver ?? new SteamPathResolver();
    }

    public async Task<GameListSnapshot> LoadGameListAsync(CancellationToken cancellationToken = default)
    {
        var steamPath = _pathResolver.Resolve();
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

        var status = userId is null
            ? $"已找到 Steam：{steamPath}，但未找到当前用户"
            : $"已找到 Steam：{steamPath}，用户 {userId}，游戏 {rows.Count} 个";

        return new GameListSnapshot(steamPath, userId, status, rows);
    }
}

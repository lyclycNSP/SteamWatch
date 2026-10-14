using SteamWatch.Core.Steam;

namespace SteamWatch.Infrastructure.Enforcement;

public sealed class GameProcessTerminator(IProcessTerminatorBackend? backend = null) : IGameProcessTerminator
{
    private readonly IProcessTerminatorBackend _backend = backend ?? new WindowsProcessTerminatorBackend();

    public GameProcessTerminationResult Terminate(int processId) => TerminateAsync(processId).GetAwaiter().GetResult();

    public Task<GameProcessTerminationResult> TerminateAsync(int processId, CancellationToken cancellationToken = default) =>
        TerminateCoreAsync(processId, null, cancellationToken);

    public Task<GameProcessTerminationResult> TerminateAsync(RunningSteamGame game, CancellationToken cancellationToken = default) =>
        TerminateCoreAsync(game.ProcessId, game, cancellationToken);

    private async Task<GameProcessTerminationResult> TerminateCoreAsync(int processId, RunningSteamGame? target, CancellationToken cancellationToken)
    {
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
        cancellationToken.ThrowIfCancellationRequested();
        var requested = false;
        var killed = false;
        try
        {
            var identity = _backend.GetIdentity(processId);
            if (target is not null && (target.ExecutablePath is null || identity is null
                || !identity.EndsWith(":" + target.ExecutablePath, StringComparison.OrdinalIgnoreCase)
                || (target.StartTimeUtcTicks is { } start && !identity.StartsWith($"{start}:"))))
                return new(processId, false, false, "进程身份与采样结果不一致，已取消关闭。");
            requested = _backend.TryCloseMainWindow(processId);
            if (requested && await _backend.WaitForExitAsync(processId, TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false))
                return new(processId, true, false, Exited: true);
            cancellationToken.ThrowIfCancellationRequested();
            if (!_backend.IsSameProcess(processId, identity))
                return new(processId, requested, false, "进程身份已改变，已取消终止操作。");
            _backend.KillProcessTree(processId);
            killed = true;
            var exited = await _backend.WaitForExitAsync(processId, TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
            return new(processId, requested, true, exited ? null : "未能确认进程退出。", exited);
        }
        catch (OperationCanceledException) { throw; }
        catch (ArgumentException) { return new(processId, requested, killed, Exited: true); }
        catch (Exception ex) { return new(processId, requested, killed, ex.Message); }
    }
}

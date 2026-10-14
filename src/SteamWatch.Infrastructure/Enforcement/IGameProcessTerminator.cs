using SteamWatch.Core.Steam;

namespace SteamWatch.Infrastructure.Enforcement;

public interface IGameProcessTerminator
{
    Task<GameProcessTerminationResult> TerminateAsync(RunningSteamGame game, CancellationToken cancellationToken = default) =>
        TerminateAsync(game.ProcessId, cancellationToken);
    GameProcessTerminationResult Terminate(int processId);
    Task<GameProcessTerminationResult> TerminateAsync(int processId, CancellationToken cancellationToken = default);
}

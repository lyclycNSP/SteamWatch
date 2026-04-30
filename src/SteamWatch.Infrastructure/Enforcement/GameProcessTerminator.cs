namespace SteamWatch.Infrastructure.Enforcement;

public sealed class GameProcessTerminator : IGameProcessTerminator
{
    private readonly IProcessTerminatorBackend _backend;

    public GameProcessTerminator(IProcessTerminatorBackend? backend = null)
    {
        _backend = backend ?? new WindowsProcessTerminatorBackend();
    }

    public GameProcessTerminationResult Terminate(int processId)
    {
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), "ProcessId must be positive.");
        }

        try
        {
            if (_backend.TryCloseMainWindow(processId))
            {
                return new GameProcessTerminationResult(processId, CloseRequested: true, Killed: false);
            }

            _backend.KillProcessTree(processId);
            return new GameProcessTerminationResult(processId, CloseRequested: false, Killed: true);
        }
        catch (Exception ex)
        {
            return new GameProcessTerminationResult(processId, CloseRequested: false, Killed: false, ex.Message);
        }
    }
}

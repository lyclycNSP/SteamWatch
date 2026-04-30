namespace SteamWatch.Infrastructure.Enforcement;

public interface IGameProcessTerminator
{
    GameProcessTerminationResult Terminate(int processId);
}

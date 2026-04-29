using SteamWatch.Core.Settings;

namespace SteamWatch.Infrastructure.Startup;

public interface IStartupManager
{
    StartupState GetState();

    void Enable(string executablePath);

    void Disable();
}

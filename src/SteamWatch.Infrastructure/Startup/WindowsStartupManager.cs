using SteamWatch.Core.Settings;

namespace SteamWatch.Infrastructure.Startup;

public sealed class WindowsStartupManager : IStartupManager
{
    public const string DefaultAppName = "SteamWatch";

    private readonly string _appName;
    private readonly IStartupRegistry _registry;

    public WindowsStartupManager(IStartupRegistry registry, string appName = DefaultAppName)
    {
        _registry = registry;
        _appName = string.IsNullOrWhiteSpace(appName)
            ? throw new ArgumentException("App name is required.", nameof(appName))
            : appName;
    }

    public StartupState GetState()
    {
        var command = _registry.Read(_appName);
        return new StartupState(!string.IsNullOrWhiteSpace(command), command);
    }

    public void Enable(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path is required.", nameof(executablePath));
        }

        _registry.Write(_appName, QuoteExecutablePath(executablePath));
    }

    public void Disable()
    {
        _registry.Delete(_appName);
    }

    public static string QuoteExecutablePath(string executablePath)
    {
        var trimmed = executablePath.Trim();
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            return trimmed;
        }

        return $"\"{trimmed}\"";
    }
}

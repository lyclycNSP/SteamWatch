using System.Diagnostics;
using Microsoft.Win32;

namespace SteamWatch.Infrastructure.Storage;

public sealed record UninstallCommand(string FileName, string[] Arguments, bool IsInno)
{
    public ProcessStartInfo CreateStartInfo()
    {
        var start = new ProcessStartInfo(FileName) { UseShellExecute = false, CreateNoWindow = !IsInno };
        foreach (var argument in Arguments) start.ArgumentList.Add(argument);
        return start;
    }
}

public static class InstalledApplication
{
    public static bool IsInstalled(string? applicationDirectory = null, string? localApplicationData = null)
    {
        var app = Path.TrimEndingDirectorySeparator(Path.GetFullPath(applicationDirectory ?? AppContext.BaseDirectory));
        var local = localApplicationData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Same(app, Path.Combine(local, "Programs", "SteamWatch")) && !File.Exists(Path.Combine(app, "portable.marker"));
    }

    public static UninstallCommand? GetUninstallCommand()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\SteamWatch_is1");
            return ResolveCommand(AppContext.BaseDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), key?.GetValue("UninstallString") as string);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null; // Settings offers reinstall/repair when registration cannot be read.
        }
    }

    public static UninstallCommand? ResolveCommand(string applicationDirectory, string localApplicationData, string? registeredCommand)
    {
        var app = Path.TrimEndingDirectorySeparator(Path.GetFullPath(applicationDirectory));
        if (!IsInstalled(app, localApplicationData) || !HasNoLinks(app)) return null;
        if (registeredCommand is { Length: > 2 } && registeredCommand[0] == '"' && registeredCommand[^1] == '"')
        {
            var path = registeredCommand[1..^1];
            try
            {
                var management = Path.GetDirectoryName(Path.GetFullPath(path));
                if (management is not null && Same(Path.GetDirectoryName(management)!, Path.Combine(localApplicationData, "Programs", "SteamWatch.Installers")) &&
                    Guid.TryParseExact(Path.GetFileName(management), "N", out _) &&
                    Path.GetFileName(path).Equals("unins000.exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path) && HasNoLinks(path))
                    return new(path, [], true);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { }
        }
        var script = Path.Combine(app, "Uninstall.ps1");
        if (File.Exists(script))
            return new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
                ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-CallerProcessId", Environment.ProcessId.ToString()], false);
        var legacy = Path.Combine(app, "SteamWatchSetup.exe");
        return File.Exists(legacy) ? new(legacy, ["--uninstall"], false) : null;
    }

    private static bool Same(string first, string second) => string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);

    private static bool HasNoLinks(string path)
    {
        try
        {
            for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return false;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
}

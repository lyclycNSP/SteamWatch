using Microsoft.Win32;
using System.Runtime.Versioning;

namespace SteamWatch.Infrastructure.Steam;

public sealed class SteamPathResolver
{
    private const string RegistryPath = @"SOFTWARE\WOW6432Node\Valve\Steam";
    private const string InstallPathValue = "InstallPath";

    [SupportedOSPlatform("windows")]
    public string Resolve()
    {
        var registryPath = TryReadRegistryPath();
        if (!string.IsNullOrWhiteSpace(registryPath))
        {
            return registryPath;
        }

        return GetCommonPaths().FirstOrDefault(Directory.Exists)
            ?? @"C:\Program Files (x86)\Steam";
    }

    public static IReadOnlyList<string> GetCommonPaths()
    {
        var paths = new List<string>();
        foreach (var drive in new[] { "C", "D", "E", "F", "G" })
        {
            paths.Add($@"{drive}:\Program Files (x86)\Steam");
            paths.Add($@"{drive}:\Program Files\Steam");
            paths.Add($@"{drive}:\Steam");
            paths.Add($@"{drive}:\Games\Steam");
            paths.Add($@"{drive}:\SteamLibrary");
        }

        return paths;
    }

    [SupportedOSPlatform("windows")]
    private static string? TryReadRegistryPath()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RegistryPath);
            return key?.GetValue(InstallPathValue) as string;
        }
        catch
        {
            return null;
        }
    }
}

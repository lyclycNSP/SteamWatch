namespace SteamWatch.Installer;

// Only the entry point selects paths. The production CLI has no profile/registry overrides.
internal sealed record InstallationContext(string ProgramDirectory, string DataDirectory,
    string ManagementRoot, string DesktopLink, string StartMenuDirectory, string LegacyKey,
    string InnoKey, string StartupKey)
{
    internal const string UninstallRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\";
    internal static InstallationContext CurrentUser()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new(Path.Combine(local, "Programs", "SteamWatch"), Path.Combine(local, "SteamWatch"),
            Path.Combine(local, "Programs", "SteamWatch.Installers"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SteamWatch.lnk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "SteamWatch"),
            UninstallRoot + "SteamWatch", UninstallRoot + "SteamWatch_is1",
            @"Software\Microsoft\Windows\CurrentVersion\Run");
    }

    internal string[] Links => [DesktopLink, Path.Combine(StartMenuDirectory, "SteamWatch.lnk"),
        Path.Combine(StartMenuDirectory, "\u5378\u8F7D SteamWatch.lnk"),
        Path.Combine(StartMenuDirectory, "\u64CD\u4F5C\u6307\u5357.lnk")];

    internal void ValidateManagement(string management)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(management));
        if (!PathSafety.Same(Path.GetDirectoryName(full)!, ManagementRoot) ||
            !Guid.TryParseExact(Path.GetFileName(full), "N", out _))
            throw new InvalidDataException("Installation management directory is outside the current-user scope.");
        PathSafety.NoLinks(full, tree: true);
    }
}

internal static class PathSafety
{
    internal static bool Same(string first, string second) => string.Equals(Path.GetFullPath(first),
        Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);

    internal static void NoLinks(string path, bool tree = false)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Installation paths cannot contain reparse points.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        if (tree && Directory.Exists(path))
            foreach (var child in Directory.EnumerateFileSystemEntries(path)) NoLinks(child, Directory.Exists(child));
    }
}

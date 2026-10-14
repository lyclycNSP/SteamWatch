namespace SteamWatch.Infrastructure.Storage;

public static class AppDataDirectory
{
    public static string Resolve(string? applicationDirectory = null, string? localApplicationData = null)
    {
        var app = Path.GetFullPath(applicationDirectory ?? AppContext.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar);
        var local = localApplicationData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var installed = Path.GetFullPath(Path.Combine(local, "Programs", "SteamWatch"));
        var portableRoot = GetPortableRoot(app);
        var portable = portableRoot is not null
            || !string.Equals(app, installed, StringComparison.OrdinalIgnoreCase);
        var destination = portable ? Path.Combine(portableRoot ?? app, "data") : Path.Combine(local, "SteamWatch");
        Directory.CreateDirectory(destination);
        if (!portable || (portableRoot is not null && !string.Equals(portableRoot, app, StringComparison.OrdinalIgnoreCase)))
        {
            var legacy = Path.Combine(app, "data");
            var marker = Path.Combine(destination, "legacy-migration.complete");
            if (Directory.Exists(legacy) && !File.Exists(marker))
            {
                foreach (var file in Directory.EnumerateFiles(legacy, "*", SearchOption.TopDirectoryOnly))
                {
                    if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
                    var target = Path.Combine(destination, Path.GetFileName(file));
                    if (!File.Exists(target)) File.Copy(file, target);
                }
                File.WriteAllText(marker, "Legacy files copied without deleting the originals.");
            }
        }
        return destination;
    }

    public static string ResolveExecutablePath(string? processPath = null)
    {
        var executable = Path.GetFullPath(processPath ?? Environment.ProcessPath
            ?? throw new InvalidOperationException("无法定位启动程序。"));
        var app = Path.GetDirectoryName(executable)!;
        var root = GetPortableRoot(app);
        var launcher = root is null ? null : Path.Combine(root, "SteamWatch.exe");
        return launcher is not null && File.Exists(launcher) ? launcher : executable;
    }

    private static string? GetPortableRoot(string app)
    {
        if (File.Exists(Path.Combine(app, "portable.marker"))) return app;
        var parent = Path.GetDirectoryName(app);
        return string.Equals(Path.GetFileName(app), "app", StringComparison.OrdinalIgnoreCase)
            && parent is not null && File.Exists(Path.Combine(parent, "portable.marker")) ? parent : null;
    }
}

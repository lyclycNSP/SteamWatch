namespace SteamWatch.Installer;

public static class DirectoryUpgrade
{
    public static void Commit(string stagingDirectory, string installDirectory, Action register)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(installDirectory))!;
        if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(stagingDirectory)), parent, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("暂存目录必须与安装目录位于同一父目录。");
        var backup = Path.Combine(parent, $"SteamWatch.previous-{Guid.NewGuid():N}");
        var hadPrevious = Directory.Exists(installDirectory);
        if (hadPrevious) Directory.Move(installDirectory, backup);
        try
        {
            Directory.Move(stagingDirectory, installDirectory);
            register();
        }
        catch
        {
            if (Directory.Exists(installDirectory)) Directory.Move(installDirectory, stagingDirectory);
            if (hadPrevious) Directory.Move(backup, installDirectory);
            throw;
        }
        // Retain the old directory as a recovery copy, including any legacy user data.
    }
}

using System.IO.Compression;

namespace SteamWatch.Installer;

internal static class PayloadExtractor
{
    internal static void Extract(string zipPath, string stage)
    {
        PathSafety.NoLinks(stage);
        if (Directory.Exists(stage)) throw new IOException("Staging directory already exists.");
        Directory.CreateDirectory(stage);
        using var zip = ZipFile.OpenRead(zipPath);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName.Replace('\\', '/');
            const string prefix = "SteamWatch-win-x64/";
            if (!name.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidDataException("Unexpected installer payload root.");
            name = name[prefix.Length..];
            if (name.Length == 0) continue;
            if (new[] { "portable.marker", "Uninstall.ps1", "SteamWatchSetup.exe" }.Contains(name, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException("Portable or legacy uninstall entry cannot be installed.");
            var parts = name.TrimEnd('/').Split('/');
            if (parts.Any(part => part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ') ||
                part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                throw new InvalidDataException("Payload contains an ambiguous or invalid Windows file name.");
            var destination = Path.GetFullPath(Path.Combine(stage, name));
            if (!destination.StartsWith(stage + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                (entry.ExternalAttributes >> 16 & 0xF000) == 0xA000 || !names.Add(destination))
                throw new InvalidDataException("Payload traversal, link or duplicate entry.");
            if (name.EndsWith('/')) { Directory.CreateDirectory(destination); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var input = entry.Open();
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }
        if (!File.Exists(Path.Combine(stage, "SteamWatch.exe"))) throw new InvalidDataException("Application entry missing.");
    }
}

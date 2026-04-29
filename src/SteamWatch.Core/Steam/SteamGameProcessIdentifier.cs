using System.Text.RegularExpressions;

namespace SteamWatch.Core.Steam;

public sealed class SteamGameProcessIdentifier
{
    private static readonly Regex SteamAppIdRegex = new("steam_app_(?<appId>\\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly HashSet<string> SteamProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "steam.exe",
        "steam"
    };
    private static readonly HashSet<string> ExcludedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "steamwebhelper.exe",
        "steamservice.exe",
        "gameoverlayui.exe",
        "gameoverlayui64.exe",
        "steamerrorreporter.exe",
        "streaming_client.exe"
    };

    private readonly IReadOnlyDictionary<int, SteamGameInfo> _knownGames;
    private readonly string? _steamPath;

    public SteamGameProcessIdentifier(IEnumerable<SteamGameInfo> knownGames, string? steamPath = null)
    {
        _knownGames = knownGames.ToDictionary(game => game.AppId);
        _steamPath = string.IsNullOrWhiteSpace(steamPath) ? null : Path.GetFullPath(steamPath);
    }

    public IReadOnlyList<RunningSteamGame> Identify(IReadOnlyCollection<SteamProcessSnapshot> processes)
    {
        var steamProcessIds = processes
            .Where(process => SteamProcessNames.Contains(process.Name))
            .Select(process => process.ProcessId)
            .ToHashSet();

        if (steamProcessIds.Count == 0)
        {
            return [];
        }

        var overlayTargetProcessId = GetOverlayTargetProcessId(processes);
        var runningGames = new Dictionary<int, RunningSteamGame>();

        foreach (var process in processes)
        {
            if (!ShouldInspect(process, steamProcessIds, overlayTargetProcessId))
            {
                continue;
            }

            var appId = ExtractAppId(process);
            if (appId is null)
            {
                continue;
            }

            runningGames[appId.Value] = new RunningSteamGame(
                appId.Value,
                process.ProcessId,
                process.Name,
                _knownGames.GetValueOrDefault(appId.Value)?.Name);
        }

        return runningGames.Values.OrderBy(game => game.AppId).ToList();
    }

    public static int? ExtractOverlayTargetProcessId(IReadOnlyList<string> commandLine)
    {
        for (var index = 0; index < commandLine.Count - 1; index++)
        {
            if (!string.Equals(commandLine[index], "-pid", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(commandLine[index + 1], out var processId))
            {
                return processId;
            }
        }

        return null;
    }

    private bool ShouldInspect(
        SteamProcessSnapshot process,
        IReadOnlySet<int> steamProcessIds,
        int? overlayTargetProcessId)
    {
        if (string.IsNullOrWhiteSpace(process.Name))
        {
            return false;
        }

        if (SteamProcessNames.Contains(process.Name) || ExcludedProcessNames.Contains(process.Name))
        {
            return false;
        }

        return steamProcessIds.Contains(process.ParentProcessId)
            || process.ProcessId == overlayTargetProcessId
            || IsKnownSteamLibraryPath(process.ExecutablePath);
    }

    private int? ExtractAppId(SteamProcessSnapshot process)
    {
        foreach (var argument in process.CommandLine)
        {
            var match = SteamAppIdRegex.Match(argument);
            if (match.Success)
            {
                return int.Parse(match.Groups["appId"].Value);
            }
        }

        return TryMatchKnownGameByPath(process.ExecutablePath);
    }

    private int? TryMatchKnownGameByPath(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        var normalizedPath = Normalize(executablePath);
        if (!normalizedPath.Contains($"{Path.DirectorySeparatorChar}steamapps{Path.DirectorySeparatorChar}common{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        foreach (var game in _knownGames.Values)
        {
            var normalizedName = NormalizeName(game.Name);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                continue;
            }

            var normalizedExecutable = NormalizeName(Path.GetFileNameWithoutExtension(executablePath));
            var normalizedDirectory = NormalizeName(Path.GetFileName(Path.GetDirectoryName(executablePath)) ?? string.Empty);

            if (normalizedExecutable.Contains(normalizedName, StringComparison.OrdinalIgnoreCase)
                || normalizedName.Contains(normalizedExecutable, StringComparison.OrdinalIgnoreCase)
                || normalizedDirectory.Contains(normalizedName, StringComparison.OrdinalIgnoreCase)
                || normalizedName.Contains(normalizedDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return game.AppId;
            }
        }

        return null;
    }

    private bool IsKnownSteamLibraryPath(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(_steamPath) || string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        var normalizedExecutable = Normalize(executablePath);
        var normalizedSteamPath = Normalize(_steamPath);
        return normalizedExecutable.StartsWith(normalizedSteamPath, StringComparison.OrdinalIgnoreCase)
            && normalizedExecutable.Contains($"{Path.DirectorySeparatorChar}steamapps{Path.DirectorySeparatorChar}common{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static int? GetOverlayTargetProcessId(IEnumerable<SteamProcessSnapshot> processes)
    {
        foreach (var process in processes)
        {
            if (process.Name.Contains("gameoverlayui", StringComparison.OrdinalIgnoreCase))
            {
                return ExtractOverlayTargetProcessId(process.CommandLine);
            }
        }

        return null;
    }

    private static string Normalize(string path)
    {
        return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private static string NormalizeName(string value)
    {
        return value
            .Replace(".exe", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(":", string.Empty)
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty)
            .Trim();
    }
}

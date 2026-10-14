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
        "streaming_client.exe",
        "crashpad_handler.exe"
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

        foreach (var process in processes.OrderByDescending(p => p.ExecutablePath.Count(c => c == Path.DirectorySeparatorChar)).ThenByDescending(p => p.ProcessId))
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
                _knownGames.GetValueOrDefault(appId.Value)?.Name, process.ExecutablePath, process.StartTimeUtcTicks);
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
                if (int.TryParse(match.Groups["appId"].Value, out var appId) && _knownGames.ContainsKey(appId)) return appId;
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

        var fullPath = Path.GetFullPath(executablePath);
        foreach (var game in _knownGames.Values)
        {
            var root = game.InstallDirectory;
            if (root is null && _steamPath is not null)
                root = Path.Combine(_steamPath, "steamapps", "common", game.Name);
            if (root is null) continue;
            var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return game.AppId;
        }
        return null;
    }

    private bool IsKnownSteamLibraryPath(string executablePath) => TryMatchKnownGameByPath(executablePath) is not null;

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

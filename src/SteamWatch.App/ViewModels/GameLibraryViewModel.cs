using SteamWatch.Core.Steam;

namespace SteamWatch.App;

public sealed class GameLibraryViewModel
{
    public string SearchText { get; set; } = string.Empty;
    public bool RunningOnly { get; set; }
    public bool RulesOnly { get; set; }
    public SteamAppCategory? Category { get; set; }
    public int? SelectedAppId { get; private set; }

    public bool Matches(int appId, string name, bool isRunning, SteamAppCategory category, bool hasRule)
    {
        var query = SearchText.Trim();
        return (query.Length == 0 || name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || appId.ToString().Contains(query))
            && (!RunningOnly || isRunning) && (!RulesOnly || hasRule)
            && (Category is null || Category == category);
    }

    public void SelectGame(int? appId) => SelectedAppId = appId;

    public int? ReconcileSelection(IEnumerable<int> visibleAppIds)
    {
        if (SelectedAppId is { } selected && !visibleAppIds.Contains(selected)) SelectedAppId = null;
        return SelectedAppId;
    }
}

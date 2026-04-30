namespace SteamWatch.App;

public sealed class PlaytimeStatRowViewModel
{
    public PlaytimeStatRowViewModel()
    {
    }

    public PlaytimeStatRowViewModel(string periodText, int appId, string gameName, int minutes)
    {
        PeriodText = periodText;
        AppId = appId;
        GameName = gameName;
        Minutes = minutes;
    }

    public string PeriodText { get; set; } = string.Empty;

    public int AppId { get; set; }

    public string GameName { get; set; } = string.Empty;

    public int Minutes { get; set; }

    public string AppIdText => $"AppID {AppId}";

    public string MinutesText => $"{Minutes} 分钟";
}

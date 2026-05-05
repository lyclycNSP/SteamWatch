using Microsoft.UI.Xaml.Media.Imaging;

namespace SteamWatch.App;

public sealed class PlaytimeStatRowViewModel
{
    public PlaytimeStatRowViewModel()
    {
    }

    public PlaytimeStatRowViewModel(string periodText, int appId, string gameName, int minutes, string? iconPath = null)
    {
        PeriodText = periodText;
        AppId = appId;
        GameName = gameName;
        Minutes = minutes;
        IconPath = iconPath;
    }

    public string PeriodText { get; set; } = string.Empty;

    public int AppId { get; set; }

    public string GameName { get; set; } = string.Empty;

    public int Minutes { get; set; }

    public string? IconPath { get; set; }

    public string AppIdText => $"AppID {AppId}";

    public string MinutesText => $"{Minutes} 分钟";

    public BitmapImage? IconSource => string.IsNullOrWhiteSpace(IconPath)
        ? null
        : new BitmapImage(new Uri(Path.GetFullPath(IconPath)));
}

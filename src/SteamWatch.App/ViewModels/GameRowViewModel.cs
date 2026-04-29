namespace SteamWatch.App;

public sealed class GameRowViewModel
{
    public GameRowViewModel()
    {
    }

    public GameRowViewModel(
        int appId,
        string name,
        int playtimeForeverMinutes,
        string status,
        string todayText,
        string weekText)
    {
        AppId = appId;
        Name = name;
        PlaytimeForeverMinutes = playtimeForeverMinutes;
        Status = status;
        TodayText = todayText;
        WeekText = weekText;
    }

    public int AppId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int PlaytimeForeverMinutes { get; set; }

    public string Status { get; set; } = string.Empty;

    public string TodayText { get; set; } = string.Empty;

    public string WeekText { get; set; } = string.Empty;

    public string AppIdText => $"AppID {AppId}";

    public string SteamPlaytimeText => $"Steam 累计 {PlaytimeForeverMinutes} 分钟";

    public GameRowViewModel WithRuntimeState(bool isRunning, int todayMinutes, int weekMinutes)
    {
        return new GameRowViewModel(
            AppId,
            Name,
            PlaytimeForeverMinutes,
            isRunning ? "运行中" : "未运行",
            $"今日 {todayMinutes} 分钟",
            $"本周 {weekMinutes} 分钟");
    }
}

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media.Imaging;

namespace SteamWatch.App;

public sealed class GameRowViewModel : INotifyPropertyChanged
{
    private int _appId;
    private string _name = string.Empty;
    private int _playtimeForeverMinutes;
    private string _status = string.Empty;
    private string _todayText = string.Empty;
    private string _weekText = string.Empty;
    private string? _iconPath;

    public GameRowViewModel()
    {
    }

    public GameRowViewModel(
        int appId,
        string name,
        int playtimeForeverMinutes,
        string status,
        string todayText,
        string weekText,
        string? iconPath = null)
    {
        AppId = appId;
        Name = name;
        PlaytimeForeverMinutes = playtimeForeverMinutes;
        Status = status;
        TodayText = todayText;
        WeekText = weekText;
        IconPath = iconPath;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public int AppId
    {
        get => _appId;
        set
        {
            if (SetProperty(ref _appId, value))
            {
                OnPropertyChanged(nameof(AppIdText));
            }
        }
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public int PlaytimeForeverMinutes
    {
        get => _playtimeForeverMinutes;
        set
        {
            if (SetProperty(ref _playtimeForeverMinutes, value))
            {
                OnPropertyChanged(nameof(SteamPlaytimeText));
            }
        }
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string TodayText
    {
        get => _todayText;
        set => SetProperty(ref _todayText, value);
    }

    public string WeekText
    {
        get => _weekText;
        set => SetProperty(ref _weekText, value);
    }

    public string? IconPath
    {
        get => _iconPath;
        set
        {
            if (SetProperty(ref _iconPath, value))
            {
                OnPropertyChanged(nameof(IconSource));
            }
        }
    }

    public string AppIdText => $"AppID {AppId}";

    public string SteamPlaytimeText => $"Steam 累计 {PlaytimeForeverMinutes} 分钟";

    public BitmapImage? IconSource => string.IsNullOrWhiteSpace(IconPath)
        ? null
        : new BitmapImage(new Uri(Path.GetFullPath(IconPath)));

    public void UpdateFrom(GameRowViewModel source)
    {
        AppId = source.AppId;
        Name = source.Name;
        PlaytimeForeverMinutes = source.PlaytimeForeverMinutes;
        Status = source.Status;
        TodayText = source.TodayText;
        WeekText = source.WeekText;
        IconPath = source.IconPath;
    }

    public GameRowViewModel WithRuntimeState(bool isRunning, int todayMinutes, int weekMinutes)
    {
        return new GameRowViewModel(
            AppId,
            Name,
            PlaytimeForeverMinutes,
            isRunning ? "运行中" : "未运行",
            $"今日 {todayMinutes} 分钟",
            $"本周 {weekMinutes} 分钟",
            IconPath);
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Media.Imaging;
using SteamWatch.Core.Steam;

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
    private string _ruleText = "未设置";
    public string RuleText { get => _ruleText; set => SetProperty(ref _ruleText, value); }
    private SteamAppCategory _category;
    public SteamAppCategory Category
    {
        get => _category;
        set { if (SetProperty(ref _category, value)) OnPropertyChanged(nameof(CategoryText)); }
    }
    public string CategoryText => Category switch
    {
        SteamAppCategory.Game => "游戏（用户分类）",
        SteamAppCategory.Tool => "运行库／工具（用户分类）",
        _ => "分类未知"
    };

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

    public int TodayMinutes { get; set; }
    public int WeekMinutes { get; set; }

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
        set { if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(SteamPlaytimeText)); }
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

    public string SteamPlaytimeText => Status == "未在当前库中" ? "Steam 累计：当前库无数据" : $"Steam 累计 {PlaytimeForeverMinutes} 分钟";

    public override string ToString() => $"{Name}，{Status}，今日实测 {TodayMinutes} 分钟，本周实测 {WeekMinutes} 分钟，单游戏限额 {RuleText}";

    public BitmapImage? IconSource => string.IsNullOrWhiteSpace(IconPath)
        ? null
        : new BitmapImage(new Uri(Path.GetFullPath(IconPath)));

    public void UpdateFrom(GameRowViewModel source)
    {
        TodayMinutes = source.TodayMinutes;
        WeekMinutes = source.WeekMinutes;
        AppId = source.AppId;
        Name = source.Name;
        PlaytimeForeverMinutes = source.PlaytimeForeverMinutes;
        Status = source.Status;
        TodayText = source.TodayText;
        WeekText = source.WeekText;
        IconPath = source.IconPath;
        Category = source.Category;
        RuleText = source.RuleText;
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

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SteamWatch.App.Services;

namespace SteamWatch.App;

public sealed partial class MainPage : Page, INotifyPropertyChanged
{
    private readonly SteamWatchAppService _appService = new();
    private readonly DispatcherTimer _monitorTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private string _statusMessage = "等待读取 Steam 缓存。";
    private string _gameCountText = "游戏 0 个";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<GameRowViewModel> GameRows { get; } = [];

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string GameCountText
    {
        get => _gameCountText;
        private set => SetProperty(ref _gameCountText, value);
    }

    public MainPage()
    {
        InitializeComponent();
        _monitorTimer.Tick += MonitorTimer_Tick;
        _ = LoadGamesAsync();
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ShowSettings();
            return;
        }

        if (args.SelectedItemContainer?.Tag is not string tag)
        {
            return;
        }

        ShowMainContent(tag);
    }

    private void ShowMainContent(string tag)
    {
        SettingsView.Visibility = Visibility.Collapsed;
        GamesView.Visibility = Visibility.Visible;

        PageTitle.Text = tag switch
        {
            "Statistics" => "统计",
            "ActivityLog" => "日志",
            _ => "游戏"
        };
        PageSubtitle.Text = "今日 0 分钟 / 本周 0 分钟";
    }

    private void ShowSettings()
    {
        GamesView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Visible;
        PageTitle.Text = "设置";
        PageSubtitle.Text = "托盘、提醒和自启动";
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadGamesAsync();
    }

    private async Task LoadGamesAsync()
    {
        RefreshButton.IsEnabled = false;
        StatusMessage = "正在读取 Steam 缓存...";

        try
        {
            var snapshot = await _appService.LoadGameListAsync();
            GameRows.Clear();
            foreach (var game in snapshot.Games)
            {
                GameRows.Add(game);
            }

            StatusMessage = snapshot.StatusText;
            GameCountText = $"游戏 {GameRows.Count} 个";
            PageSubtitle.Text = $"游戏 {GameRows.Count} 个 / 今日 0 分钟 / 本周 0 分钟";
            _monitorTimer.Start();
            RefreshRuntimeStatus();
        }
        catch (Exception ex)
        {
            StatusMessage = $"读取 Steam 缓存失败：{ex.Message}";
            GameCountText = "游戏 0 个";
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    private void MonitorTimer_Tick(object? sender, object e)
    {
        RefreshRuntimeStatus();
    }

    private void RefreshRuntimeStatus()
    {
        try
        {
            var snapshot = _appService.PollRuntimeStatus(DateTimeOffset.Now);
            ReplaceGameRows(snapshot.Games);
            StatusMessage = snapshot.StatusText;

            var totalToday = snapshot.Games.Sum(game => ExtractMinutes(game.TodayText));
            var totalWeek = snapshot.Games.Sum(game => ExtractMinutes(game.WeekText));
            PageSubtitle.Text = $"游戏 {GameRows.Count} 个 / 今日 {totalToday} 分钟 / 本周 {totalWeek} 分钟";
        }
        catch (Exception ex)
        {
            StatusMessage = $"监控刷新失败：{ex.Message}";
        }
    }

    private void ReplaceGameRows(IEnumerable<GameRowViewModel> games)
    {
        GameRows.Clear();
        foreach (var game in games)
        {
            GameRows.Add(game);
        }
    }

    private static int ExtractMinutes(string text)
    {
        var digits = new string(text.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var minutes) ? minutes : 0;
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

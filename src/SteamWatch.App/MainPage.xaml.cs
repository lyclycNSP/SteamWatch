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

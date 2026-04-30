using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SteamWatch.App.Services;
using SteamWatch.Core.Limits;
using SteamWatch.Core.Notifications;
using SteamWatch.Core.Settings;

namespace SteamWatch.App;

public sealed partial class MainPage : Page, INotifyPropertyChanged
{
    private readonly SteamWatchAppService _appService = new();
    private readonly DispatcherTimer _monitorTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly string _firstRunGuideMarkerPath = Path.Combine(AppContext.BaseDirectory, "data", "first-run-guide-shown.txt");
    private readonly string _guidePath = Path.Combine(AppContext.BaseDirectory, "操作指南.txt");
    private string _statusMessage = "等待读取 Steam 缓存。";
    private string _gameCountText = "游戏 0 个";
    private bool _isRefreshingRuntimeStatus;
    private bool _isMonitoringPaused;
    private bool _isShowingNotificationDialog;
    private LimitRule? _selectedLimitRule;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<GameRowViewModel> GameRows { get; } = [];

    public ObservableCollection<LimitRuleRowViewModel> LimitRows { get; } = [];

    public ObservableCollection<PlaytimeStatRowViewModel> DailyStatRows { get; } = [];

    public ObservableCollection<PlaytimeStatRowViewModel> WeeklyStatRows { get; } = [];

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
        _appService.UserNotificationRaised += AppService_UserNotificationRaised;
        _monitorTimer.Tick += MonitorTimer_Tick;
        Loaded += MainPage_Loaded;
        _ = LoadSettingsAsync();
        _ = LoadGamesAsync();
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainPage_Loaded;
        _ = ShowFirstRunGuideAsync();
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
        GamesView.Visibility = tag == "Games" ? Visibility.Visible : Visibility.Collapsed;
        StatisticsView.Visibility = tag == "Statistics" ? Visibility.Visible : Visibility.Collapsed;

        PageTitle.Text = tag switch
        {
            "Statistics" => "统计",
            _ => "游戏"
        };
        if (tag == "Statistics")
        {
            RefreshStatistics();
        }

        PageSubtitle.Text = tag == "Statistics"
            ? GetStatisticsSubtitle()
            : "今日 0 分钟 / 本周 0 分钟";
    }

    private void ShowSettings()
    {
        GamesView.Visibility = Visibility.Collapsed;
        StatisticsView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Visible;
        PageTitle.Text = "设置";
        PageSubtitle.Text = "托盘、提醒和自启动";
    }

    public void NavigateToSettings()
    {
        ShowSettings();
    }

    public void SetMonitoringPaused(bool isPaused)
    {
        _isMonitoringPaused = isPaused;
        PauseMonitoringButton.Label = isPaused ? "恢复监控" : "暂停监控";
        PauseMonitoringButton.Icon = new SymbolIcon(isPaused ? Symbol.Play : Symbol.Pause);
        if (isPaused)
        {
            _monitorTimer.Stop();
            StatusMessage = "监控已暂停。";
            return;
        }

        _monitorTimer.Start();
        StatusMessage = "监控已恢复。";
        _ = RefreshRuntimeStatusAsync();
    }

    private void AppService_UserNotificationRaised(object? sender, NotificationMessage message)
    {
        _ = DispatcherQueue.TryEnqueue(() => _ = ShowNotificationDialogAsync(message));
    }

    private async Task ShowNotificationDialogAsync(NotificationMessage message)
    {
        if (_isShowingNotificationDialog || XamlRoot is null)
        {
            return;
        }

        _isShowingNotificationDialog = true;
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = message.Title,
                Content = message.Body,
                CloseButtonText = "知道了"
            };

            await dialog.ShowAsync();
        }
        finally
        {
            _isShowingNotificationDialog = false;
        }
    }

    private async Task ShowFirstRunGuideAsync()
    {
        if (File.Exists(_firstRunGuideMarkerPath) || XamlRoot is null)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_firstRunGuideMarkerPath)!);
        await File.WriteAllTextAsync(_firstRunGuideMarkerPath, DateTimeOffset.Now.ToString("O"));

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "欢迎使用 SteamWatch",
            Content = "安装目录中包含《操作指南.txt》。建议先查看通知设置、托盘退出方式和游戏限额说明。",
            PrimaryButtonText = File.Exists(_guidePath) ? "打开操作指南" : string.Empty,
            CloseButtonText = "稍后查看"
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && File.Exists(_guidePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _guidePath,
                UseShellExecute = true
            });
        }
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
            ReplaceGameRows(snapshot.Games);
            ReplaceLimitRows(_appService.GetLimitRules());
            RefreshStatistics();

            StatusMessage = snapshot.StatusText;
            GameCountText = $"游戏 {GameRows.Count} 个";
            PageSubtitle.Text = $"游戏 {GameRows.Count} 个 / 今日 0 分钟 / 本周 0 分钟";
            _monitorTimer.Start();
            await RefreshRuntimeStatusAsync();
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
        _ = RefreshRuntimeStatusAsync();
    }

    private void SetLimitForSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        LimitScopeComboBox.SelectedIndex = 1;
        LimitEnforcementComboBox.SelectedIndex = 0;
        LimitMinutesNumberBox.Focus(FocusState.Programmatic);
    }

    private void PauseMonitoringButton_Click(object sender, RoutedEventArgs e)
    {
        SetMonitoringPaused(!_isMonitoringPaused);
    }

    private void ForceClosePolicyButton_Click(object sender, RoutedEventArgs e)
    {
        LimitScopeComboBox.SelectedIndex = 1;
        LimitEnforcementComboBox.SelectedIndex = 1;
        LimitMinutesNumberBox.Focus(FocusState.Programmatic);
    }

    private void GamesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selectedLimitRule is null)
        {
            return;
        }

        LimitRulesListView.SelectedItem = null;
        ClearSelectedLimitRule();
        StatusMessage = "已切换为新建限额规则。";
    }

    private void LimitRulesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LimitRulesListView.SelectedItem is not LimitRuleRowViewModel { Rule: { } rule })
        {
            ClearSelectedLimitRule();
            return;
        }

        _selectedLimitRule = rule;
        DeleteLimitRuleButton.IsEnabled = true;
        StatusMessage = "已选中规则，可点击删除规则；点击左侧游戏列表会切换为新建规则。";
    }

    private async void DeleteLimitRuleButton_Click(object sender, RoutedEventArgs e)
    {
        var rule = _selectedLimitRule;
        if (rule is null)
        {
            return;
        }

        try
        {
            var rules = await _appService.DeleteLimitRuleAsync(rule);
            ReplaceLimitRows(rules);
            ClearSelectedLimitRule();
            StatusMessage = "限额规则已删除。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"删除限额失败：{ex.Message}";
        }
    }

    private async void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (double.IsNaN(ForceCloseCountdownNumberBox.Value))
        {
            StatusMessage = "请输入有效的强退倒计时秒数。";
            return;
        }

        if (double.IsNaN(FirstReminderThresholdNumberBox.Value)
            || double.IsNaN(SecondReminderThresholdNumberBox.Value)
            || double.IsNaN(FinalReminderThresholdNumberBox.Value))
        {
            StatusMessage = "请输入有效的提醒阈值。";
            return;
        }

        var firstThreshold = (int)Math.Round(FirstReminderThresholdNumberBox.Value);
        var secondThreshold = (int)Math.Round(SecondReminderThresholdNumberBox.Value);
        var finalThreshold = (int)Math.Round(FinalReminderThresholdNumberBox.Value);
        if (firstThreshold <= 0 || firstThreshold >= secondThreshold || secondThreshold >= finalThreshold || finalThreshold >= 100)
        {
            StatusMessage = "提醒阈值必须满足：一级 < 二级 < 最终 < 100。";
            return;
        }

        var settings = new AppSettings(
            CloseWindowActionComboBox.SelectedIndex == 1
                ? CloseWindowAction.ExitApplication
                : CloseWindowAction.MinimizeToTray,
            StartWithWindowsSwitch.IsOn,
            PlayReminderSoundsSwitch.IsOn,
            Math.Max(5, (int)Math.Round(ForceCloseCountdownNumberBox.Value)),
            firstThreshold,
            secondThreshold,
            finalThreshold);

        try
        {
            await _appService.SaveSettingsAsync(settings);
            ApplySettings(settings);
            StatusMessage = "设置已保存。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存设置失败：{ex.Message}";
        }
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            ApplySettings(await _appService.LoadSettingsAsync());
        }
        catch (Exception ex)
        {
            StatusMessage = $"读取设置失败：{ex.Message}";
        }
    }

    private async void SaveLimitButton_Click(object sender, RoutedEventArgs e)
    {
        var scope = LimitScopeComboBox.SelectedIndex == 1 ? LimitScope.Game : LimitScope.Global;
        var selectedGame = GamesListView.SelectedItem as GameRowViewModel;
        if (scope == LimitScope.Game && selectedGame is null)
        {
            StatusMessage = "请先在左侧选择一个游戏。";
            return;
        }

        if (double.IsNaN(LimitMinutesNumberBox.Value))
        {
            StatusMessage = "请输入有效的限额分钟数。";
            return;
        }

        var minutes = (int)Math.Round(LimitMinutesNumberBox.Value);
        if (minutes <= 0)
        {
            StatusMessage = "限额分钟数必须大于 0。";
            return;
        }

        var period = LimitPeriodComboBox.SelectedIndex == 1 ? LimitPeriod.Week : LimitPeriod.Day;
        var enforcement = LimitEnforcementComboBox.SelectedIndex == 1
            ? EnforcementMode.ForceClose
            : EnforcementMode.NotifyOnly;
        var rule = new LimitRule(
            scope,
            period,
            minutes,
            enforcement,
            selectedGame?.AppId,
            scope == LimitScope.Global ? "全部游戏" : selectedGame?.Name ?? string.Empty);

        try
        {
            var rules = await _appService.UpsertLimitRuleAsync(rule);
            ReplaceLimitRows(rules);
            LimitRulesListView.SelectedItem = null;
            ClearSelectedLimitRule();
            StatusMessage = "限额规则已保存。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存限额失败：{ex.Message}";
        }
    }

    private async Task RefreshRuntimeStatusAsync()
    {
        if (_isMonitoringPaused || _isRefreshingRuntimeStatus)
        {
            return;
        }

        _isRefreshingRuntimeStatus = true;
        try
        {
            var snapshot = await _appService.PollRuntimeStatusAsync(DateTimeOffset.Now);
            ReplaceGameRows(snapshot.Games);
            StatusMessage = snapshot.StatusText;

            var totalToday = snapshot.Games.Sum(game => ExtractMinutes(game.TodayText));
            var totalWeek = snapshot.Games.Sum(game => ExtractMinutes(game.WeekText));
            RefreshStatistics();
            PageSubtitle.Text = StatisticsView.Visibility == Visibility.Visible
                ? GetStatisticsSubtitle()
                : $"游戏 {GameRows.Count} 个 / 今日 {totalToday} 分钟 / 本周 {totalWeek} 分钟";
        }
        catch (Exception ex)
        {
            StatusMessage = $"监控刷新失败：{ex.Message}";
        }
        finally
        {
            _isRefreshingRuntimeStatus = false;
        }
    }

    private void ReplaceGameRows(IEnumerable<GameRowViewModel> games)
    {
        var selectedAppId = (GamesListView.SelectedItem as GameRowViewModel)?.AppId;
        var nextRows = games.ToList();
        var existingByAppId = GameRows.ToDictionary(game => game.AppId);

        for (var index = 0; index < nextRows.Count; index++)
        {
            var incoming = nextRows[index];
            var row = existingByAppId.TryGetValue(incoming.AppId, out var existing)
                ? existing
                : incoming;

            if (!ReferenceEquals(row, incoming))
            {
                row.UpdateFrom(incoming);
            }

            if (index >= GameRows.Count)
            {
                GameRows.Add(row);
                continue;
            }

            if (ReferenceEquals(GameRows[index], row))
            {
                continue;
            }

            var existingIndex = GameRows.IndexOf(row);
            if (existingIndex >= 0)
            {
                GameRows.Move(existingIndex, index);
            }
            else
            {
                GameRows.Insert(index, row);
            }
        }

        while (GameRows.Count > nextRows.Count)
        {
            GameRows.RemoveAt(GameRows.Count - 1);
        }

        if (selectedAppId is not null)
        {
            GamesListView.SelectedItem = GameRows.FirstOrDefault(game => game.AppId == selectedAppId.Value);
        }
    }

    private void ReplaceLimitRows(IEnumerable<LimitRule> rules)
    {
        LimitRows.Clear();
        foreach (var rule in rules
            .Where(rule => rule.IsEnabled)
            .OrderBy(rule => rule.Scope)
            .ThenBy(rule => rule.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(rule => rule.Period))
        {
            LimitRows.Add(new LimitRuleRowViewModel(rule));
        }
    }

    private void ClearSelectedLimitRule()
    {
        _selectedLimitRule = null;
        SaveLimitButton.Content = "保存限额";
        DeleteLimitRuleButton.IsEnabled = false;
    }

    private void ApplySettings(AppSettings settings)
    {
        CloseWindowActionComboBox.SelectedIndex = settings.CloseWindowAction == CloseWindowAction.ExitApplication ? 1 : 0;
        StartWithWindowsSwitch.IsOn = settings.StartWithWindows;
        PlayReminderSoundsSwitch.IsOn = settings.PlayReminderSounds;
        ForceCloseCountdownNumberBox.Value = settings.ForceCloseCountdownSeconds;
        FirstReminderThresholdNumberBox.Value = settings.FirstReminderThresholdPercent;
        SecondReminderThresholdNumberBox.Value = settings.SecondReminderThresholdPercent;
        FinalReminderThresholdNumberBox.Value = settings.FinalReminderThresholdPercent;
    }

    private void RefreshStatistics()
    {
        ReplaceStatRows(DailyStatRows, _appService.GetDailyStats());
        ReplaceStatRows(WeeklyStatRows, _appService.GetWeeklyStats());
    }

    private string GetStatisticsSubtitle()
    {
        return $"每日记录 {DailyStatRows.Count} 条 / 每周汇总 {WeeklyStatRows.Count} 条";
    }

    private static void ReplaceStatRows(
        ObservableCollection<PlaytimeStatRowViewModel> target,
        IEnumerable<PlaytimeStatRowViewModel> source)
    {
        target.Clear();
        foreach (var row in source)
        {
            target.Add(row);
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

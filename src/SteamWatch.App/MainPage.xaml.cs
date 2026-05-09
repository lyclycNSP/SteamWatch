using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SteamWatch.App.Services;
using SteamWatch.Core.Limits;
using SteamWatch.Core.Notifications;
using SteamWatch.Core.Security;
using SteamWatch.Core.Settings;
using SteamWatch.Core.Tracking;

namespace SteamWatch.App;

public sealed partial class MainPage : Page, INotifyPropertyChanged
{
    private readonly SteamWatchAppService _appService = new();
    private readonly DispatcherTimer _monitorTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly string _firstRunGuideMarkerPath = Path.Combine(AppContext.BaseDirectory, "data", "first-run-guide-shown.txt");
    private string _statusMessage = "等待读取 Steam 缓存。";
    private string _gameCountText = "游戏 0 个";
    private bool _isRefreshingRuntimeStatus;
    private bool _isMonitoringPaused;
    private bool _isShowingNotificationDialog;
    private bool _isApplyingLimitRuleSelection;
    private bool _isInitializingStatisticsFilters;
    private bool _isApplyingSettings;
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
        InitializeStatisticsFilters();
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
        HelpView.Visibility = tag == "Help" ? Visibility.Visible : Visibility.Collapsed;

        PageTitle.Text = tag switch
        {
            "Statistics" => "统计",
            "Help" => "帮助",
            _ => "游戏"
        };
        if (tag == "Statistics")
        {
            RefreshStatistics();
        }

        PageSubtitle.Text = tag == "Statistics"
            ? GetStatisticsSubtitle()
            : tag == "Help"
                ? "操作指南和常见设置"
                : "今日 0 分钟 / 本周 0 分钟";
    }

    private void ShowSettings()
    {
        GamesView.Visibility = Visibility.Collapsed;
        StatisticsView.Visibility = Visibility.Collapsed;
        HelpView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Visible;
        PageTitle.Text = "设置";
        PageSubtitle.Text = "选择设置类别";
        ShowSettingsRoot();
    }

    public void NavigateToSettings()
    {
        ShowSettings();
    }

    public AppSettings GetCurrentSettings()
    {
        return _appService.GetSettings();
    }

    public CloseWindowAction GetCurrentCloseWindowAction()
    {
        return CloseWindowActionComboBox.SelectedIndex == 1
            ? CloseWindowAction.ExitApplication
            : CloseWindowAction.MinimizeToTray;
    }

    public Task<bool> AuthorizeSensitiveActionAsync(string actionName)
    {
        return _appService.IsAuthenticationRequired()
            ? ShowAuthenticationDialogAsync(actionName)
            : Task.FromResult(true);
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
            Content = "建议先查看帮助页面，了解通知设置、托盘退出方式、游戏限额和认证保护。",
            PrimaryButtonText = "查看帮助",
            CloseButtonText = "稍后查看"
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            RootNavigation.SelectedItem = HelpNavigationItem;
            ShowMainContent("Help");
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
            if (GamesView.Visibility == Visibility.Visible)
            {
                PageSubtitle.Text = $"游戏 {GameRows.Count} 个 / 今日 0 分钟 / 本周 0 分钟";
            }

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

    private void DailyStatsDatePicker_DateChanged(object sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_isInitializingStatisticsFilters)
        {
            return;
        }

        RefreshStatistics();
        if (StatisticsView.Visibility == Visibility.Visible)
        {
            PageSubtitle.Text = GetStatisticsSubtitle();
        }
    }

    private void WeeklyStatsDatePicker_DateChanged(object sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_isInitializingStatisticsFilters)
        {
            return;
        }

        RefreshStatistics();
        if (StatisticsView.Visibility == Visibility.Visible)
        {
            PageSubtitle.Text = GetStatisticsSubtitle();
        }
    }

    private void SettingsSectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string section })
        {
            ShowSettingsSection(section);
        }
    }

    private void SettingsBackButton_Click(object sender, RoutedEventArgs e)
    {
        ShowSettingsRoot();
    }

    private async void ImmediateSettingsSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        await SaveImmediateGeneralSettingsAsync();
    }

    private async void ImmediateSettingsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await SaveImmediateGeneralSettingsAsync();
    }

    private void GamesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingLimitRuleSelection)
        {
            return;
        }

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
        ApplyLimitRuleToForm(rule);
        SaveLimitButton.Content = "更新规则";
        DeleteLimitRuleButton.IsEnabled = true;
        StatusMessage = "已选中规则，可修改限额分钟或超限策略后保存；点击左侧游戏列表会切换为新建规则。";
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
            if (!await AuthorizeSensitiveActionAsync("删除限额规则"))
            {
                StatusMessage = "未通过认证，限额规则未删除。";
                return;
            }

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
        var existingSettings = _appService.GetSettings();
        var existingAuthenticationRequired = existingSettings.RequireAuthenticationForSensitiveActions
            && existingSettings.AuthenticationCredential is not null;
        if (existingAuthenticationRequired
            && !await ShowAuthenticationDialogAsync("保存设置"))
        {
            ShowSettingsMessage("未通过认证，设置未保存。", InfoBarSeverity.Warning);
            return;
        }

        if (double.IsNaN(ForceCloseCountdownNumberBox.Value))
        {
            ShowSettingsMessage("请输入有效的强退倒计时秒数。", InfoBarSeverity.Error);
            return;
        }

        if (double.IsNaN(FirstReminderThresholdNumberBox.Value)
            || double.IsNaN(SecondReminderThresholdNumberBox.Value)
            || double.IsNaN(FinalReminderThresholdNumberBox.Value))
        {
            ShowSettingsMessage("请输入有效的提醒阈值。", InfoBarSeverity.Error);
            return;
        }

        var firstThreshold = (int)Math.Round(FirstReminderThresholdNumberBox.Value);
        var secondThreshold = (int)Math.Round(SecondReminderThresholdNumberBox.Value);
        var finalThreshold = (int)Math.Round(FinalReminderThresholdNumberBox.Value);
        if (firstThreshold <= 0 || firstThreshold >= secondThreshold || secondThreshold >= finalThreshold || finalThreshold >= 100)
        {
            ShowSettingsMessage("提醒阈值必须满足：一级 < 二级 < 最终 < 100。", InfoBarSeverity.Error);
            return;
        }

        var authenticationCredential = existingSettings.AuthenticationCredential;
        if (RequireAuthenticationSwitch.IsOn)
        {
            var password = AuthenticationPasswordBox.Password;
            var confirmPassword = ConfirmAuthenticationPasswordBox.Password;
            if (!string.IsNullOrEmpty(password) || !string.IsNullOrEmpty(confirmPassword))
            {
                if (password.Length < 4)
                {
                    ShowSettingsMessage("认证密码至少需要 4 位。", InfoBarSeverity.Error);
                    return;
                }

                if (password != confirmPassword)
                {
                    ShowSettingsMessage("两次输入的认证密码不一致，认证保护没有保存。", InfoBarSeverity.Error);
                    return;
                }

                authenticationCredential = PasswordHasher.Create(password);
            }
            else if (authenticationCredential is null)
            {
                ShowSettingsMessage("开启认证时需要设置密码。", InfoBarSeverity.Error);
                return;
            }
        }
        else
        {
            authenticationCredential = null;
        }

        var settings = new AppSettings(
            GetCurrentCloseWindowAction(),
            StartWithWindowsSwitch.IsOn,
            PlayReminderSoundsSwitch.IsOn,
            Math.Max(5, (int)Math.Round(ForceCloseCountdownNumberBox.Value)),
            firstThreshold,
            secondThreshold,
            finalThreshold,
            RequireAuthenticationSwitch.IsOn,
            authenticationCredential);

        try
        {
            await _appService.SaveSettingsAsync(settings);
            ApplySettings(settings);
            AuthenticationPasswordBox.Password = string.Empty;
            ConfirmAuthenticationPasswordBox.Password = string.Empty;
            ShowSettingsMessage("设置已保存。", InfoBarSeverity.Success);
            StatusMessage = "设置已保存。";
        }
        catch (Exception ex)
        {
            ShowSettingsMessage($"保存设置失败：{ex.Message}", InfoBarSeverity.Error);
            StatusMessage = $"保存设置失败：{ex.Message}";
        }
    }

    private async void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (!await AuthorizeSensitiveActionAsync("卸载 SteamWatch"))
        {
            ShowSettingsMessage("未通过认证，未启动卸载。", InfoBarSeverity.Warning);
            return;
        }

        var installerPath = Path.Combine(AppContext.BaseDirectory, "SteamWatchSetup.exe");
        if (!File.Exists(installerPath))
        {
            ShowSettingsMessage("当前目录未找到 SteamWatchSetup.exe。只有安装器版支持从应用内一键卸载。", InfoBarSeverity.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "--uninstall",
            UseShellExecute = true
        });
        App.ExitApplication();
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

        if (!await AuthorizeSensitiveActionAsync("保存限额规则"))
        {
            StatusMessage = "未通过认证，限额规则未保存。";
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
            var selectedRule = _selectedLimitRule;
            var rules = selectedRule is null
                ? await _appService.UpsertLimitRuleAsync(rule)
                : await _appService.ReplaceLimitRuleAsync(selectedRule, rule);
            ReplaceLimitRows(rules);
            LimitRulesListView.SelectedItem = null;
            ClearSelectedLimitRule();
            StatusMessage = selectedRule is null ? "限额规则已保存。" : "限额规则已更新。";
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
                : HelpView.Visibility == Visibility.Visible
                    ? "操作指南和常见设置"
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

    private void ApplyLimitRuleToForm(LimitRule rule)
    {
        _isApplyingLimitRuleSelection = true;
        try
        {
            LimitScopeComboBox.SelectedIndex = rule.Scope == LimitScope.Game ? 1 : 0;
            LimitPeriodComboBox.SelectedIndex = rule.Period == LimitPeriod.Week ? 1 : 0;
            LimitMinutesNumberBox.Value = rule.MaxMinutes;
            LimitEnforcementComboBox.SelectedIndex = rule.Enforcement == EnforcementMode.ForceClose ? 1 : 0;

            if (rule.Scope == LimitScope.Game && rule.AppId is int appId)
            {
                GamesListView.SelectedItem = GameRows.FirstOrDefault(game => game.AppId == appId);
            }
        }
        finally
        {
            _isApplyingLimitRuleSelection = false;
        }
    }

    private void ApplySettings(AppSettings settings)
    {
        _isApplyingSettings = true;
        try
        {
            CloseWindowActionComboBox.SelectedIndex = settings.CloseWindowAction == CloseWindowAction.ExitApplication ? 1 : 0;
            StartWithWindowsSwitch.IsOn = settings.StartWithWindows;
            PlayReminderSoundsSwitch.IsOn = settings.PlayReminderSounds;
            ForceCloseCountdownNumberBox.Value = settings.ForceCloseCountdownSeconds;
            FirstReminderThresholdNumberBox.Value = settings.FirstReminderThresholdPercent;
            SecondReminderThresholdNumberBox.Value = settings.SecondReminderThresholdPercent;
            FinalReminderThresholdNumberBox.Value = settings.FinalReminderThresholdPercent;
            RequireAuthenticationSwitch.IsOn = settings.RequireAuthenticationForSensitiveActions;
            AuthenticationPasswordBox.PlaceholderText = settings.AuthenticationCredential is null
                ? "设置认证密码"
                : "留空则保留当前密码";
        }
        finally
        {
            _isApplyingSettings = false;
        }
    }

    private void ShowSettingsMessage(string message, InfoBarSeverity severity)
    {
        SettingsInfoBar.Title = message;
        SettingsInfoBar.Message = string.Empty;
        SettingsInfoBar.Severity = severity;
        SettingsInfoBar.IsOpen = true;
    }

    private void ShowSettingsRoot()
    {
        SettingsRootPanel.Visibility = Visibility.Visible;
        GeneralSettingsPanel.Visibility = Visibility.Collapsed;
        ReminderSettingsPanel.Visibility = Visibility.Collapsed;
        AuthenticationSettingsPanel.Visibility = Visibility.Collapsed;
        MaintenanceSettingsPanel.Visibility = Visibility.Collapsed;
        PageSubtitle.Text = "选择设置类别";
    }

    private void ShowSettingsSection(string section)
    {
        SettingsRootPanel.Visibility = Visibility.Collapsed;
        GeneralSettingsPanel.Visibility = section == "General" ? Visibility.Visible : Visibility.Collapsed;
        ReminderSettingsPanel.Visibility = section == "Reminder" ? Visibility.Visible : Visibility.Collapsed;
        AuthenticationSettingsPanel.Visibility = section == "Authentication" ? Visibility.Visible : Visibility.Collapsed;
        MaintenanceSettingsPanel.Visibility = section == "Maintenance" ? Visibility.Visible : Visibility.Collapsed;
        PageSubtitle.Text = section switch
        {
            "General" => "常规设置会立即生效",
            "Reminder" => "修改后点击保存设置",
            "Authentication" => "修改后点击保存设置",
            "Maintenance" => "应用维护",
            _ => "选择设置类别"
        };
    }

    private async Task SaveImmediateGeneralSettingsAsync()
    {
        if (_isApplyingSettings || !IsLoaded)
        {
            return;
        }

        var existingSettings = _appService.GetSettings();
        var settings = existingSettings with
        {
            CloseWindowAction = GetCurrentCloseWindowAction(),
            StartWithWindows = StartWithWindowsSwitch.IsOn,
            PlayReminderSounds = PlayReminderSoundsSwitch.IsOn
        };

        try
        {
            await _appService.SaveSettingsAsync(settings);
            ApplySettings(settings);
            ShowSettingsMessage("常规设置已保存。", InfoBarSeverity.Success);
            StatusMessage = "常规设置已保存。";
        }
        catch (Exception ex)
        {
            ShowSettingsMessage($"保存常规设置失败：{ex.Message}", InfoBarSeverity.Error);
            StatusMessage = $"保存常规设置失败：{ex.Message}";
        }
    }

    private async Task<bool> ShowAuthenticationDialogAsync(string actionName)
    {
        if (XamlRoot is null)
        {
            return false;
        }

        var passwordBox = new PasswordBox
        {
            Header = "认证密码",
            MinWidth = 280
        };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = actionName,
            Content = passwordBox,
            PrimaryButtonText = "确认",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary
            && _appService.VerifyAuthentication(passwordBox.Password);
    }

    private void RefreshStatistics()
    {
        var selectedDate = GetSelectedDate(DailyStatsDatePicker);
        var selectedWeekStart = WeekCalculator.GetWeekStart(GetSelectedDate(WeeklyStatsDatePicker));
        var selectedWeekEnd = WeekCalculator.GetWeekEnd(selectedWeekStart);

        ReplaceStatRows(DailyStatRows, _appService.GetDailyStats(selectedDate));
        ReplaceStatRows(WeeklyStatRows, _appService.GetWeeklyStats(selectedWeekStart));

        DailyStatsTotalText.Text = $"{selectedDate:yyyy-MM-dd} 总时长 {DailyStatRows.Sum(row => row.Minutes)} 分钟";
        WeeklyStatsTotalText.Text = $"{selectedWeekStart:yyyy-MM-dd} - {selectedWeekEnd:yyyy-MM-dd} 总时长 {WeeklyStatRows.Sum(row => row.Minutes)} 分钟";
    }

    private string GetStatisticsSubtitle()
    {
        return "按日期和周查看游玩统计";
    }

    private void InitializeStatisticsFilters()
    {
        _isInitializingStatisticsFilters = true;
        try
        {
            var today = DateTimeOffset.Now;
            DailyStatsDatePicker.Date = today;
            WeeklyStatsDatePicker.Date = today;
        }
        finally
        {
            _isInitializingStatisticsFilters = false;
        }
    }

    private static DateOnly GetSelectedDate(CalendarDatePicker picker)
    {
        var date = picker.Date ?? DateTimeOffset.Now;
        return DateOnly.FromDateTime(date.LocalDateTime);
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

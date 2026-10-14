using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SteamWatch.App.Services;
using SteamWatch.Core.Limits;
using SteamWatch.Core.Notifications;
using SteamWatch.Core.Settings;
using SteamWatch.Core.Steam;

namespace SteamWatch.App;

public sealed partial class MainPage : Page, INotifyPropertyChanged
{
    private readonly SteamWatchAppService _appService = App.Service;
    private readonly DispatcherTimer _monitorTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly string _firstRunGuideMarkerPath = Path.Combine(App.Service.DataDirectory, "first-run-guide-shown.txt");
    private string _statusMessage = "等待读取 Steam 缓存。";
    private readonly Presentation.UiOperationCoordinator _operations = new();
    private readonly Presentation.PresentationLifetime _lifetime = new();
    private Task? _initializationTask;
    private bool _hasLoadedRuntime;
    private EventHandler<NotificationMessage>? _notificationHandler;
    public bool IsMonitoringPaused => _appService.IsPaused;
    private bool _isShuttingDown;
    private bool _isInitializing = true;
    private readonly Presentation.ExitPreparation _exitPreparation = new();
    private readonly SemaphoreSlim _dialogGate = new(1, 1);
    private (NotificationMessage Message, int Generation)? _pendingNotification;
    private ContentDialog? _notificationDialog;
    private int _notificationDialogGeneration;
    private bool _notificationPresentationRunning;
    private object? _currentNavigationItem;
    private bool _isRestoringNavigation;
    private bool _isNavigating;

    public event PropertyChangedEventHandler? PropertyChanged;




    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            SetProperty(ref _statusMessage, value);
            GameEditor?.ShowStatus(value);
        }
    }

    public MainPage()
    {
        InitializeComponent();
        GameEditor.Configure(_appService, AuthorizeSensitiveActionAsync, ShowDialogAsync, RunUserOperationAsync);
        GameEditor.StatusChanged += (_, status) => StatusMessage = status;
        SettingsEditor.Configure(AuthorizeSensitiveActionAsync, ShowDialogAsync);
        SettingsEditor.MaintenanceRequested += SettingsEditor_MaintenanceRequested;
        HelpEditor.SettingsRequested += HelpEditor_SettingsRequested;
        SettingsEditor.SubtitleChanged += (_, subtitle) =>
        {
            if (SettingsEditor.Visibility == Visibility.Visible) PageSubtitle.Text = subtitle;
        };
        SettingsEditor.StatusChanged += (_, message) => StatusMessage = message;
        RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
        StatisticsEditor.Configure(_appService, ShowDialogAsync);
        StatisticsEditor.EstimatedDetailsRequested += StatisticsEditor_EstimatedDetailsRequested;
        StatisticsEditor.SubtitleChanged += (_, subtitle) =>
        {
            if (StatisticsEditor.Visibility == Visibility.Visible) PageSubtitle.Text = subtitle;
        };
        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;
    }

    private async Task InitializeAsync()
    {
        try
        {
            IsEnabled = false;
            SettingsEditor.ApplySettings((await _appService.InitializeAsync()).Settings);
            IsEnabled = true;
            await LoadGamesAsync();
        }
        catch (Exception ex) { StatusMessage = $"启动失败：{ex.Message}"; }
        finally { IsEnabled = true; _isInitializing = false; }
    }

    public async Task<bool> PrepareForExitAsync(string actionName = "退出 SteamWatch", CancellationToken cancellationToken = default)
    {
        if (_isInitializing || _exitPreparation.IsActive || _isNavigating || SettingsEditor.IsNavigating
            || SettingsEditor.HasPendingSave || GameEditor.IsEditingRule || _dialogGate.CurrentCount == 0
            || !_operations.TrySuspend())
        {
            StatusMessage = "当前操作尚未完成，请完成后重试退出。";
            return false;
        }
        RootNavigation.IsEnabled = false;
        try
        {
            return await _exitPreparation.TryPrepareAsync(SettingsEditor.ConfirmExitAsync,
                () => AuthorizeSensitiveActionAsync(actionName), async () =>
                {
                    _isShuttingDown = true;
                    _monitorTimer.Stop();
                    await _appService.ShutdownAsync();
                }, ResumeAfterExitFailure, cancellationToken);
        }
        finally
        {
            if (!_exitPreparation.IsActive)
            {
                _operations.Resume();
                RootNavigation.IsEnabled = true;
                SettingsEditor.RestoreEditorFocus();
            }
        }
    }

    public void ResumeAfterExitFailure()
    {
        _appService.ResumeAfterExitFailure();
        _exitPreparation.Reset();
        RootNavigation.IsEnabled = true;
        _isShuttingDown = false;
        _operations.Resume();
        if (_lifetime.IsAttached) _monitorTimer.Start();
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_lifetime.Attach()) return;
        var generation = _lifetime.Generation;
        _notificationHandler = (_, message) => QueueNotification(message, generation);
        _appService.UserNotificationRaised += _notificationHandler;
        _monitorTimer.Tick += MonitorTimer_Tick;
        var reattaching = _initializationTask is not null;
        await (_initializationTask ??= InitializeAsync());
        if (!_lifetime.IsCurrent(generation) || _isShuttingDown || _operations.IsSuspended) return;
        // The initial load owns both its snapshot and error feedback. Do not replace a
        // failed startup with an empty "not initialized" presentation.
        if (!_hasLoadedRuntime) return;
        if (reattaching) ApplySnapshot(_appService.CaptureReadModel());
        _monitorTimer.Start();
        await ShowFirstRunGuideAsync();
    }

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!_lifetime.Detach()) return;
        _monitorTimer.Stop();
        _monitorTimer.Tick -= MonitorTimer_Tick;
        _appService.UserNotificationRaised -= _notificationHandler;
        _notificationHandler = null;
        _pendingNotification = null;
        _notificationDialog?.Hide();
    }

    private async void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_isRestoringNavigation || args.SelectedItem is null) return;
        var target = args.SelectedItem;
        if (Equals(target, _currentNavigationItem)) return;
        _isRestoringNavigation = true;
        sender.SelectedItem = _currentNavigationItem;
        _isRestoringNavigation = false;
        if (_exitPreparation.IsActive || _operations.IsBusy || _isNavigating || SettingsEditor.IsNavigating) return;
        _isNavigating = true;
        try
        {
            if (!await SettingsEditor.ConfirmNavigationAsync())
            {
                SettingsEditor.RestoreEditorFocus();
                return;
            }
            _currentNavigationItem = target;
            _isRestoringNavigation = true;
            sender.SelectedItem = target;
            _isRestoringNavigation = false;
            if (Equals(target, sender.SettingsItem)) ShowSettings();
            else if (target is NavigationViewItem { Tag: string tag }) ShowMainContent(tag);
        }
        finally { _isNavigating = false; }
    }

    private void ShowMainContent(string tag)
    {
        SettingsEditor.Visibility = Visibility.Collapsed;
        GameEditor.Visibility = tag == "Games" ? Visibility.Visible : Visibility.Collapsed;
        StatisticsEditor.Visibility = tag == "Statistics" ? Visibility.Visible : Visibility.Collapsed;
        HelpEditor.Visibility = tag == "Help" ? Visibility.Visible : Visibility.Collapsed;

        PageTitle.Text = tag switch
        {
            "Statistics" => "统计",
            "Help" => "帮助",
            _ => "游戏"
        };
        if (tag == "Statistics")
        {
            StatisticsEditor.Refresh();
        }

        PageSubtitle.Text = tag == "Statistics"
            ? StatisticsEditor.ViewModel.Subtitle
            : tag == "Help"
                ? "操作指南和常见设置"
                : GameEditor.Subtitle;
    }

    private void ShowSettings()
    {
        GameEditor.Visibility = Visibility.Collapsed;
        StatisticsEditor.Visibility = Visibility.Collapsed;
        HelpEditor.Visibility = Visibility.Collapsed;
        SettingsEditor.Visibility = Visibility.Visible;
        PageTitle.Text = "设置";
        PageSubtitle.Text = "选择设置类别";
        SettingsEditor.ShowRoot();
    }

    public void NavigateToSettings()
    {
        RootNavigation.SelectedItem = RootNavigation.SettingsItem;
    }

    public AppSettings GetCurrentSettings()
    {
        return _appService.GetSettings();
    }

    public CloseWindowAction GetCurrentCloseWindowAction()
    {
        return _appService.GetSettings().CloseWindowAction;
    }

    public Task<bool> AuthorizeSensitiveActionAsync(string actionName)
    {
        return AuthorizeAfterInitializationAsync(actionName);
    }

    private async Task<bool> AuthorizeAfterInitializationAsync(string actionName)
    {
        await _appService.InitializeAsync();
        return !_appService.IsAuthenticationRequired() || await ShowAuthenticationDialogAsync(actionName);
    }

    public async Task SetMonitoringPausedAsync(bool isPaused)
    {
        if (_exitPreparation.IsActive) return;
        await RunUserOperationAsync(async () =>
        {
            if (!await AuthorizeSensitiveActionAsync(isPaused ? "暂停限制" : "恢复限制")) return;
            await _appService.SetPausedAsync(isPaused);
        });
    }

    private void QueueNotification(NotificationMessage message, int generation)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (!_lifetime.IsCurrent(generation) || _operations.IsSuspended) return;
            if (_notificationDialog is not null && _notificationDialogGeneration == generation)
            {
                _notificationDialog.Title = message.Title;
                _notificationDialog.Content = Presentation.DialogText.Format(message.Body);
                return;
            }
            // Only the newest reminder waits behind an editing/authentication dialog.
            _pendingNotification = (message, generation);
            if (!_notificationPresentationRunning) _ = ShowLatestNotificationDialogAsync();
        });
    }

    private async Task ShowLatestNotificationDialogAsync()
    {
        _notificationPresentationRunning = true;
        await _dialogGate.WaitAsync();
        try
        {
            var pending = _pendingNotification;
            _pendingNotification = null;
            if (pending is not { } latest || XamlRoot is null
                || !_lifetime.IsCurrent(latest.Generation) || _operations.IsSuspended) return;
            _notificationDialogGeneration = latest.Generation;
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = latest.Message.Title,
                Content = Presentation.DialogText.Format(latest.Message.Body),
                CloseButtonText = "知道了"
            };
            _notificationDialog = dialog;
            dialog.Closing += (_, _) => _notificationDialog = null;
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            _appService.RecordDiagnostic("notification-dialog", ex);
            StatusMessage = "应用内提醒显示失败，请查看当前限额状态。";
        }
        finally
        {
            _notificationDialog = null;
            _dialogGate.Release();
            _notificationPresentationRunning = false;
            if (_pendingNotification is not null) _ = ShowLatestNotificationDialogAsync();
        }
    }

    private Task ShowFirstRunGuideAsync()
    {
        if (!File.Exists(_firstRunGuideMarkerPath))
            StatusMessage = "先设置每日总限额，再按需设置单游戏规则。操作说明可在帮助与关于查看。";
        return Task.CompletedTask;
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadGamesAsync();
    }

    private async Task LoadGamesAsync()
    {
        if (SettingsEditor.HasPendingSave || _isNavigating || SettingsEditor.IsNavigating) return;
        await _operations.TryRunAsync(async () =>
        {
            RefreshButton.IsEnabled = false;
            StatusMessage = "正在读取 Steam 缓存...";
            try
            {
                var snapshot = await _appService.LoadGameListAsync();
                _hasLoadedRuntime = true;
                ApplySnapshot(snapshot);
                if (_lifetime.IsAttached) _monitorTimer.Start();
            }
            catch (Exception ex) { StatusMessage = $"读取 Steam 缓存失败：{ex.Message}"; }
            finally { RefreshButton.IsEnabled = true; }
        });
    }

    private void MonitorTimer_Tick(object? sender, object e)
    {
        _ = RefreshRuntimeStatusAsync();
    }

    private async void PauseMonitoringButton_Click(object sender, RoutedEventArgs e)
    {
        try { await SetMonitoringPausedAsync(!IsMonitoringPaused); }
        catch (Exception ex) { StatusMessage = $"切换限制失败：{ex.Message}"; }
    }

    private async Task RefreshRuntimeStatusAsync()
    {
        if (_isShuttingDown || !_lifetime.IsAttached) return;
        await _operations.TryRunAsync(() =>
        {
            try { ApplySnapshot(_appService.CaptureReadModel()); }
            catch (Exception ex) { StatusMessage = $"监控刷新失败：{ex.Message}"; }
            return Task.CompletedTask;
        });
    }

    private void ApplySnapshot(Presentation.RuntimeReadModel snapshot)
    {
        if (!_lifetime.IsAttached) return;
        GameEditor.ApplySnapshot(snapshot);
        StatisticsEditor.Refresh(snapshot);
        StatusMessage = snapshot.StatusText;
        PauseMonitoringButton.Label = snapshot.Source.IsPaused ? "恢复限制" : "暂停限制";
        PauseMonitoringButton.Icon = new SymbolIcon(snapshot.Source.IsPaused ? Symbol.Play : Symbol.Pause);
        if (SettingsEditor.Visibility != Visibility.Visible)
            PageSubtitle.Text = StatisticsEditor.Visibility == Visibility.Visible
                ? StatisticsEditor.ViewModel.Subtitle
                : HelpEditor.Visibility == Visibility.Visible ? "操作指南和常见设置" : GameEditor.Subtitle;
    }

    private async Task RunUserOperationAsync(Func<Task> action)
    {
        if (!_lifetime.IsAttached || _isInitializing || _isShuttingDown
            || SettingsEditor.HasPendingSave || _isNavigating || SettingsEditor.IsNavigating
            || !await _operations.TryRunAsync(async () =>
            {
                var previousStatus = StatusMessage;
                var settingsEnabled = SettingsEditor.IsEnabled;
                SettingsEditor.IsEnabled = false;
                try { await action(); }
                finally
                {
                    SettingsEditor.IsEnabled = settingsEnabled;
                    var feedback = StatusMessage;
                    if (!_isShuttingDown && !_operations.IsSuspended)
                    {
                        ApplySnapshot(_appService.CaptureReadModel());
                        if (feedback != previousStatus) StatusMessage = feedback;
                    }
                }
            }))
        {
            const string message = "当前操作尚未完成，请完成后重试。";
            StatusMessage = message;
            if (SettingsEditor.Visibility == Visibility.Visible)
                SettingsEditor.ShowMessage(message, InfoBarSeverity.Warning);
        }
    }

    private void ContentGrid_SizeChanged(object sender, SizeChangedEventArgs e) =>
        GameEditor?.UpdateViewport(e.NewSize.Width, e.NewSize.Height);

    public async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dialog)
    {
        await _dialogGate.WaitAsync();
        try
        {
            dialog.Content = Presentation.DialogText.Format(dialog.Content);
            return await dialog.ShowAsync();
        }
        finally { _dialogGate.Release(); }
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

        var result = await ShowDialogAsync(dialog);
        return result == ContentDialogResult.Primary
            && _appService.VerifyAuthentication(passwordBox.Password);
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

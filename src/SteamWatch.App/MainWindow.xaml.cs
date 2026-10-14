using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using SteamWatch.App.Services;
using SteamWatch.Core.Settings;
using SteamWatch.Infrastructure.Storage;
using WinRT.Interop;

namespace SteamWatch.App;

public sealed partial class MainWindow : Window
{
    private readonly AppSettingsStore _settingsStore;
    private readonly IntPtr _windowHandle;
    private readonly TrayIconService _trayIcon;
    private bool _isExiting;
    private bool _exitRequested;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        RootFrame.Navigate(typeof(MainPage));

        _settingsStore = new AppSettingsStore(
            new JsonFileStore(App.Service.DataDirectory));
        _windowHandle = WindowNative.GetWindowHandle(this);
        _trayIcon = new TrayIconService(
            "SteamWatch",
            Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"),
            () => DispatcherQueue.TryEnqueue(ShowWindow),
            () => DispatcherQueue.TryEnqueue(ToggleMonitoringPaused),
            () => DispatcherQueue.TryEnqueue(ShowSettings),
            () => DispatcherQueue.TryEnqueue(ExitFromTray),
            () => App.Service.IsPaused);
        AppWindow.Closing += AppWindow_Closing;
        Closed += MainWindow_Closed;
    }

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExiting)
        {
            return;
        }

        if (GetCloseWindowAction() == CloseWindowAction.ExitApplication)
        {
            args.Cancel = true;
            _ = RequestExitAsync();
            return;
        }

        args.Cancel = true;
        HideWindow();
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        _trayIcon.Dispose();
    }

    public void RestoreWindow() => ShowWindow();

    public Task<bool> PrepareForUpgradeAsync(CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            if (_exitRequested) { completion.TrySetResult(false); return; }
            _exitRequested = true;
            var prepared = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                ShowWindow();
                if (RootFrame.Content is not MainPage page
                    || !await page.PrepareForExitAsync("退出 SteamWatch", cancellationToken))
                { completion.TrySetResult(false); return; }
                prepared = true;
                cancellationToken.ThrowIfCancellationRequested();
                completion.TrySetResult(true);
            }
            catch (Exception ex)
            {
                if (prepared) { CancelUpgradeExit(); prepared = false; }
                completion.TrySetException(ex);
            }
            finally { if (!prepared) _exitRequested = false; }
        })) completion.TrySetException(new InvalidOperationException("窗口暂时无法处理升级请求。"));
        return completion.Task;
    }

    public void CompleteUpgradeExit()
    {
        _isExiting = true;
        _trayIcon.Dispose();
        App.ExitApplication();
    }

    public void CancelUpgradeExit()
    {
        _exitRequested = false;
        if (RootFrame.Content is MainPage page) page.ResumeAfterExitFailure();
    }

    private void ShowWindow()
    {
        WindowNativeMethods.ShowWindow(_windowHandle, WindowNativeMethods.SwShow);
        Activate();
        WindowNativeMethods.SetForegroundWindow(_windowHandle);
    }

    private void HideWindow()
    {
        WindowNativeMethods.ShowWindow(_windowHandle, WindowNativeMethods.SwHide);
    }

    private void ShowSettings()
    {
        ShowWindow();
        if (RootFrame.Content is MainPage page)
        {
            page.NavigateToSettings();
        }
    }

    private async void ToggleMonitoringPaused()
    {
        if (RootFrame.Content is MainPage page)
        {
            try { await page.SetMonitoringPausedAsync(!page.IsMonitoringPaused); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex.GetType().Name); ShowWindow(); }
        }
    }

    private void ExitFromTray()
    {
        _ = RequestExitAsync();
    }

    private async Task RequestExitAsync()
    {
        if (_exitRequested) return;
        _exitRequested = true;
        try
        {
            ShowWindow();
            if (RootFrame.Content is not MainPage page || !await page.PrepareForExitAsync()) return;
            _isExiting = true;
            _trayIcon.Dispose();
            App.ExitApplication();
        }
        catch (Exception ex)
        {
            if (RootFrame.Content is MainPage page)
                await page.ShowDialogAsync(new ContentDialog { XamlRoot = page.XamlRoot, Title = "退出前保存失败",
                    Content = $"数据尚未确认保存，应用继续运行。{ex.Message}", CloseButtonText = "知道了" });
        }
        finally { _exitRequested = false; }
    }

    private CloseWindowAction GetCloseWindowAction()
    {
        if (RootFrame.Content is MainPage page)
        {
            return page.GetCurrentCloseWindowAction();
        }

        return LoadSettings().CloseWindowAction;
    }

    private AppSettings LoadSettings()
    {
        if (RootFrame.Content is MainPage page)
        {
            return page.GetCurrentSettings();
        }

        try
        {
            return _settingsStore.LoadAsync().GetAwaiter().GetResult();
        }
        catch
        {
            return new AppSettings();
        }
    }
}

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
    private bool _isMonitoringPaused;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        RootFrame.Navigate(typeof(MainPage));

        _settingsStore = new AppSettingsStore(
            new JsonFileStore(Path.Combine(AppContext.BaseDirectory, "data")));
        _windowHandle = WindowNative.GetWindowHandle(this);
        _trayIcon = new TrayIconService(
            "SteamWatch",
            Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"),
            () => DispatcherQueue.TryEnqueue(ShowWindow),
            () => DispatcherQueue.TryEnqueue(ToggleMonitoringPaused),
            () => DispatcherQueue.TryEnqueue(ShowSettings),
            () => DispatcherQueue.TryEnqueue(ExitFromTray),
            () => _isMonitoringPaused);
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

    private void ToggleMonitoringPaused()
    {
        _isMonitoringPaused = !_isMonitoringPaused;
        if (RootFrame.Content is MainPage page)
        {
            page.SetMonitoringPaused(_isMonitoringPaused);
        }
    }

    private void ExitFromTray()
    {
        _ = RequestExitAsync();
    }

    private async Task RequestExitAsync()
    {
        ShowWindow();
        if (!await AuthorizeExitAsync())
        {
            return;
        }

        _isExiting = true;
        _trayIcon.Dispose();
        App.ExitApplication();
    }

    private async Task<bool> AuthorizeExitAsync()
    {
        if (RootFrame.Content is MainPage page)
        {
            return await page.AuthorizeSensitiveActionAsync("退出 SteamWatch");
        }

        return true;
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

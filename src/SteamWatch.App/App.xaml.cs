using Microsoft.UI.Xaml;
using SteamWatch.App.Services;
using Microsoft.Windows.AppNotifications;
using SteamWatch.Infrastructure.Runtime;

namespace SteamWatch.App;

public partial class App : Application
{
    private Window? _window;
    public static IntPtr WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(((App)Current)._window!);
    private SingleInstanceService? _instance;
    private UpgradeExitServer? _upgradeExit;
    private static readonly Lazy<SteamWatchAppService> ServiceFactory = new(() => new SteamWatchAppService());
    public static SteamWatchAppService Service => ServiceFactory.Value;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _instance = new SingleInstanceService(Service.DataDirectory);
            if (!_instance.IsPrimary) { _instance.Dispose(); Current.Exit(); return; }
            TryRegisterNotifications();
            _window = new MainWindow();
            _window.Activate();
#if UI_ACCEPTANCE
            UiAcceptanceHooks.Start((MainWindow)_window);
#endif
            if (_window is MainWindow upgradeWindow)
                _upgradeExit = new UpgradeExitServer(Service.DataDirectory, upgradeWindow.PrepareForUpgradeAsync,
                    () =>
                    {
                        if (!upgradeWindow.DispatcherQueue.TryEnqueue(upgradeWindow.CompleteUpgradeExit))
                            throw new InvalidOperationException("窗口无法完成升级退出。");
                    },
                    () => upgradeWindow.DispatcherQueue.TryEnqueue(upgradeWindow.CancelUpgradeExit),
                    ex => Service.RecordDiagnostic("upgrade", ex));
            _instance.Listen(() => _window.DispatcherQueue.TryEnqueue(() =>
            {
                if (_window is MainWindow main) main.RestoreWindow();
            }));
        }
        catch (Exception ex)
        {
            _instance?.Dispose();
            MessageBoxW(IntPtr.Zero, $"启动失败：{ex.Message}\n请确认程序与数据目录可写，或从备份恢复配置。", "SteamWatch", 0x10);
            Current.Exit();
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);

    public static void ExitApplication()
    {
        ((App)Current)._upgradeExit?.Dispose();
        ((App)Current)._instance?.Dispose();
        TryUnregisterNotifications();
        Current.Exit();
    }

    private static void TryRegisterNotifications()
    {
        try
        {
            AppNotificationManager.Default.Register();
        }
        catch
        {
            // Notification registration can fail in constrained environments; in-app dialogs still work.
        }
    }

    private static void TryUnregisterNotifications()
    {
        try
        {
            AppNotificationManager.Default.Unregister();
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }
}

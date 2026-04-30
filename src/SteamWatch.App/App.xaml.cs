using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;

namespace SteamWatch.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        TryRegisterNotifications();
        _window = new MainWindow();
        _window.Activate();
    }

    public static void ExitApplication()
    {
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

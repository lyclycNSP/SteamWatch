using Microsoft.Windows.AppNotifications;
using SteamWatch.Core.Notifications;
using SteamWatch.Infrastructure.Notifications;

namespace SteamWatch.App.Services;

public sealed class WindowsAppNotificationService : INotificationService
{
    private readonly WindowsToastNotificationService _fallback = new();
    private readonly ToastXmlFactory _xmlFactory = new();
    private readonly ISoundService _soundService = new WindowsSoundService();

    public void Show(NotificationMessage message)
    {
        try
        {
            AppNotificationManager.Default.Show(new AppNotification(_xmlFactory.Create(message)));
            if (message.PlaySound)
            {
                _soundService.Play(message.Severity);
            }
        }
        catch
        {
            _fallback.Show(message);
        }
    }
}

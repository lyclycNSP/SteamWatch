using SteamWatch.Core.Notifications;

namespace SteamWatch.Infrastructure.Notifications;

public interface INotificationService
{
    void Show(NotificationMessage message);
}

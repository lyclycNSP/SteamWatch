using SteamWatch.Core.Notifications;

namespace SteamWatch.Infrastructure.Notifications;

public interface ISoundService
{
    void Play(NotificationSeverity severity);
}

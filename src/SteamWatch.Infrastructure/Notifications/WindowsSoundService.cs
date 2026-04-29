using System.Media;
using SteamWatch.Core.Notifications;

namespace SteamWatch.Infrastructure.Notifications;

public sealed class WindowsSoundService : ISoundService
{
    public void Play(NotificationSeverity severity)
    {
        var sound = severity switch
        {
            NotificationSeverity.Critical => SystemSounds.Hand,
            NotificationSeverity.Warning => SystemSounds.Exclamation,
            _ => SystemSounds.Asterisk
        };

        sound.Play();
    }
}

using System.Security;
using SteamWatch.Core.Notifications;

namespace SteamWatch.Infrastructure.Notifications;

public sealed class ToastXmlFactory
{
    public string Create(NotificationMessage message)
    {
        var title = SecurityElement.Escape(message.Title) ?? string.Empty;
        var body = SecurityElement.Escape(message.Body) ?? string.Empty;
        var scenario = message.Severity == NotificationSeverity.Critical
            ? " scenario=\"urgent\""
            : string.Empty;

        return $"""
            <toast{scenario}>
              <visual>
                <binding template="ToastGeneric">
                  <text>{title}</text>
                  <text>{body}</text>
                </binding>
              </visual>
            </toast>
            """;
    }
}

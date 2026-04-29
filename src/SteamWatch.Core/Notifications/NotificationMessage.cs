namespace SteamWatch.Core.Notifications;

public sealed record NotificationMessage(
    string Title,
    string Body,
    NotificationSeverity Severity = NotificationSeverity.Information,
    bool PlaySound = true);

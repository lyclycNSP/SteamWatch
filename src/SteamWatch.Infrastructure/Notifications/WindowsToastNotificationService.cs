using SteamWatch.Core.Notifications;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace SteamWatch.Infrastructure.Notifications;

public sealed class WindowsToastNotificationService : INotificationService
{
    private readonly string _appUserModelId;
    private readonly ISoundService _soundService;
    private readonly ToastXmlFactory _xmlFactory;

    public WindowsToastNotificationService(
        string appUserModelId = "SteamWatch",
        ISoundService? soundService = null,
        ToastXmlFactory? xmlFactory = null)
    {
        _appUserModelId = string.IsNullOrWhiteSpace(appUserModelId)
            ? throw new ArgumentException("AppUserModelId is required.", nameof(appUserModelId))
            : appUserModelId;
        _soundService = soundService ?? new WindowsSoundService();
        _xmlFactory = xmlFactory ?? new ToastXmlFactory();
    }

    public void Show(NotificationMessage message)
    {
        var xml = new XmlDocument();
        xml.LoadXml(_xmlFactory.Create(message));

        var toast = new ToastNotification(xml);
        ToastNotificationManager.CreateToastNotifier(_appUserModelId).Show(toast);

        if (message.PlaySound)
        {
            _soundService.Play(message.Severity);
        }
    }
}

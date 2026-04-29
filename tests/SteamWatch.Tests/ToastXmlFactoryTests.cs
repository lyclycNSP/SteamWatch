using SteamWatch.Core.Notifications;
using SteamWatch.Infrastructure.Notifications;

namespace SteamWatch.Tests;

[TestClass]
public sealed class ToastXmlFactoryTests
{
    [TestMethod]
    public void Create_EscapesUserVisibleText()
    {
        var message = new NotificationMessage("SteamWatch <提醒>", "CS2 & Dota");

        var xml = new ToastXmlFactory().Create(message);

        StringAssert.Contains(xml, "SteamWatch &lt;提醒&gt;");
        StringAssert.Contains(xml, "CS2 &amp; Dota");
    }

    [TestMethod]
    public void Create_CriticalMessage_UsesUrgentScenario()
    {
        var message = new NotificationMessage("超限", "请休息", NotificationSeverity.Critical);

        var xml = new ToastXmlFactory().Create(message);

        StringAssert.Contains(xml, "scenario=\"urgent\"");
    }
}

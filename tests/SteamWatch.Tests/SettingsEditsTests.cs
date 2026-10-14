using SteamWatch.App.Presentation;
using SteamWatch.Core.Security;
using SteamWatch.Core.Settings;

namespace SteamWatch.Tests;

[TestClass]
public sealed class SettingsEditsTests
{
    [TestMethod]
    public void GeneralSaveRetainsLatestReminderAndAuthenticationSettings()
    {
        var current = new AppSettings(ForceCloseCountdownSeconds: 120,
            RequireAuthenticationForSensitiveActions: true, AuthenticationCredential: PasswordHasher.Create("fixture-password"));
        var result = new GeneralSettingsEdit(CloseWindowAction.ExitApplication, false, false).Merge(current);
        Assert.AreEqual(120, result.ForceCloseCountdownSeconds);
        Assert.IsTrue(result.RequireAuthenticationForSensitiveActions);
        Assert.AreSame(current.AuthenticationCredential, result.AuthenticationCredential);
        Assert.AreEqual(CloseWindowAction.ExitApplication, result.CloseWindowAction);
        Assert.IsFalse(result.PlayReminderSounds);
    }

    [TestMethod]
    public void ReminderSaveRetainsLatestGeneralAndAuthenticationSettings()
    {
        var current = new AppSettings(StartWithWindows: true, PlayReminderSounds: false,
            RequireAuthenticationForSensitiveActions: true, AuthenticationCredential: PasswordHasher.Create("fixture-password"));
        var result = new ReminderSettingsEdit(90, 60, 80, 95).Merge(current);
        Assert.IsTrue(result.StartWithWindows);
        Assert.IsFalse(result.PlayReminderSounds);
        Assert.AreSame(current.AuthenticationCredential, result.AuthenticationCredential);
        Assert.AreEqual(90, result.ForceCloseCountdownSeconds);
        Assert.AreEqual(60, result.FirstReminderThresholdPercent);
    }

    [TestMethod]
    public void InvalidDraftCannotProduceSettings()
    {
        ReminderSettingsEdit[] invalid = [new(double.NaN, 70, 85, 95), new(double.PositiveInfinity, 70, 85, 95),
            new(4, 70, 85, 95), new(601, 70, 85, 95), new(60.5, 70, 85, 95),
            new(60, 85, 70, 95), new(60, 70, 70, 95), new(60, 70, 85, 100), new(60, double.NaN, 85, 95)];
        foreach (var draft in invalid)
            Assert.ThrowsExactly<ArgumentException>(() => draft.Merge(new AppSettings()));
    }
}

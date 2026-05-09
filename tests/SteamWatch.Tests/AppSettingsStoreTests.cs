using SteamWatch.Core.Settings;
using SteamWatch.Core.Security;
using SteamWatch.Infrastructure.Storage;

namespace SteamWatch.Tests;

[TestClass]
public sealed class AppSettingsStoreTests
{
    [TestMethod]
    public async Task LoadAsync_MissingFile_ReturnsDefaults()
    {
        var store = new AppSettingsStore(new JsonFileStore(CreateTempDirectory()));

        var settings = await store.LoadAsync();

        Assert.AreEqual(CloseWindowAction.MinimizeToTray, settings.CloseWindowAction);
        Assert.IsFalse(settings.StartWithWindows);
        Assert.IsTrue(settings.PlayReminderSounds);
        Assert.AreEqual(60, settings.ForceCloseCountdownSeconds);
        Assert.AreEqual(70, settings.FirstReminderThresholdPercent);
        Assert.AreEqual(85, settings.SecondReminderThresholdPercent);
        Assert.AreEqual(95, settings.FinalReminderThresholdPercent);
        Assert.IsFalse(settings.RequireAuthenticationForSensitiveActions);
        Assert.IsNull(settings.AuthenticationCredential);
    }

    [TestMethod]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsSettings()
    {
        var store = new AppSettingsStore(new JsonFileStore(CreateTempDirectory()));
        var settings = new AppSettings(
            CloseWindowAction.ExitApplication,
            StartWithWindows: true,
            PlayReminderSounds: false,
            ForceCloseCountdownSeconds: 30,
            FirstReminderThresholdPercent: 50,
            SecondReminderThresholdPercent: 75,
            FinalReminderThresholdPercent: 90,
            RequireAuthenticationForSensitiveActions: true,
            AuthenticationCredential: PasswordHasher.Create("1234"));

        await store.SaveAsync(settings);
        var loaded = await store.LoadAsync();

        Assert.AreEqual(settings, loaded);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "SteamWatch.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

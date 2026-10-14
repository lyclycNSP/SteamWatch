using SteamWatch.App.Presentation;

namespace SteamWatch.Tests;

[TestClass]
public sealed class PresentationLifetimeTests
{
    [TestMethod]
    public void UnattachedViewRejectsCallbacks()
    {
        var lifetime = new PresentationLifetime();
        Assert.IsFalse(lifetime.IsCurrent(lifetime.Generation));
        Assert.IsFalse(lifetime.Detach());
    }

    [TestMethod]
    public void RepeatedLoadDoesNotDuplicateSubscriptionGeneration()
    {
        var lifetime = new PresentationLifetime();
        Assert.IsTrue(lifetime.Attach());
        var generation = lifetime.Generation;
        Assert.IsFalse(lifetime.Attach());
        Assert.IsTrue(lifetime.IsCurrent(generation));
        Assert.AreEqual(generation, lifetime.Generation);
    }

    [TestMethod]
    public void UnloadInvalidatesQueuedWorkAndDetachesOnce()
    {
        var lifetime = new PresentationLifetime();
        lifetime.Attach();
        var queued = lifetime.Generation;
        Assert.IsTrue(lifetime.Detach());
        Assert.IsFalse(lifetime.IsCurrent(queued));
        Assert.IsFalse(lifetime.Detach());
    }

    [TestMethod]
    public void ReloadCannotReviveQueuedNotificationsFromPreviousAttachment()
    {
        var lifetime = new PresentationLifetime();
        lifetime.Attach();
        var stale = lifetime.Generation;
        lifetime.Detach(); lifetime.Attach();
        Assert.IsFalse(lifetime.IsCurrent(stale));
        Assert.IsTrue(lifetime.IsCurrent(lifetime.Generation));
        Assert.AreNotEqual(stale, lifetime.Generation);
    }
}

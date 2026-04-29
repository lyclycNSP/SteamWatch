using SteamWatch.Core.Tracking;

namespace SteamWatch.Tests;

[TestClass]
public sealed class RunningGameSessionTests
{
    [TestMethod]
    public void Checkpoint_LessThanOneMinute_ReturnsZeroMinutes()
    {
        var startedAt = new DateTimeOffset(2026, 4, 29, 20, 0, 0, TimeSpan.Zero);
        var session = new RunningGameSession(730, 1234, "Counter-Strike 2", startedAt);

        var increment = session.Checkpoint(startedAt.AddSeconds(30));

        Assert.AreEqual(0, increment.Minutes);
        Assert.IsFalse(increment.IsFinal);
    }

    [TestMethod]
    public void Checkpoint_AccumulatesRemainderAcrossCalls()
    {
        var startedAt = new DateTimeOffset(2026, 4, 29, 20, 0, 0, TimeSpan.Zero);
        var session = new RunningGameSession(730, 1234, "Counter-Strike 2", startedAt);

        var first = session.Checkpoint(startedAt.AddSeconds(45));
        var second = session.Checkpoint(startedAt.AddSeconds(80));

        Assert.AreEqual(0, first.Minutes);
        Assert.AreEqual(1, second.Minutes);
    }

    [TestMethod]
    public void Complete_RoundsPositiveRemainderToOneFinalMinute()
    {
        var startedAt = new DateTimeOffset(2026, 4, 29, 20, 0, 0, TimeSpan.Zero);
        var session = new RunningGameSession(730, 1234, "Counter-Strike 2", startedAt);

        var final = session.Complete(startedAt.AddSeconds(30));

        Assert.AreEqual(1, final.Minutes);
        Assert.IsTrue(final.IsFinal);
        Assert.IsTrue(session.IsCompleted);
    }

    [TestMethod]
    public void Complete_AfterCommittedMinutes_IncludesFinalPartialMinute()
    {
        var startedAt = new DateTimeOffset(2026, 4, 29, 20, 0, 0, TimeSpan.Zero);
        var session = new RunningGameSession(730, 1234, "Counter-Strike 2", startedAt);

        var checkpoint = session.Checkpoint(startedAt.AddMinutes(5).AddSeconds(20));
        var final = session.Complete(startedAt.AddMinutes(5).AddSeconds(45));

        Assert.AreEqual(5, checkpoint.Minutes);
        Assert.AreEqual(1, final.Minutes);
    }

    [TestMethod]
    public void Checkpoint_AfterComplete_Throws()
    {
        var startedAt = new DateTimeOffset(2026, 4, 29, 20, 0, 0, TimeSpan.Zero);
        var session = new RunningGameSession(730, 1234, "Counter-Strike 2", startedAt);
        session.Complete(startedAt.AddMinutes(1));

        Assert.ThrowsExactly<InvalidOperationException>(() => session.Checkpoint(startedAt.AddMinutes(2)));
    }

    [TestMethod]
    public void Constructor_InvalidAppId_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new RunningGameSession(0, 1234, "Invalid", DateTimeOffset.UtcNow));
    }
}

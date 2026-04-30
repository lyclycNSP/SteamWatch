using SteamWatch.Infrastructure.Enforcement;

namespace SteamWatch.Tests;

[TestClass]
public sealed class GameProcessTerminatorTests
{
    [TestMethod]
    public void Terminate_WhenMainWindowCloses_DoesNotKillProcessTree()
    {
        var backend = new FakeProcessTerminatorBackend(closeMainWindowResult: true);
        var terminator = new GameProcessTerminator(backend);

        var result = terminator.Terminate(1234);

        Assert.IsTrue(result.CloseRequested);
        Assert.IsFalse(result.Killed);
        Assert.AreEqual(1, backend.CloseAttempts);
        Assert.AreEqual(0, backend.KillAttempts);
    }

    [TestMethod]
    public void Terminate_WhenMainWindowCloseFails_KillsProcessTree()
    {
        var backend = new FakeProcessTerminatorBackend(closeMainWindowResult: false);
        var terminator = new GameProcessTerminator(backend);

        var result = terminator.Terminate(1234);

        Assert.IsFalse(result.CloseRequested);
        Assert.IsTrue(result.Killed);
        Assert.AreEqual(1, backend.CloseAttempts);
        Assert.AreEqual(1, backend.KillAttempts);
    }

    [TestMethod]
    public void Terminate_WhenBackendThrows_ReturnsFailure()
    {
        var backend = new FakeProcessTerminatorBackend(closeMainWindowResult: false, throwOnKill: true);
        var terminator = new GameProcessTerminator(backend);

        var result = terminator.Terminate(1234);

        Assert.IsFalse(result.IsSuccess);
        Assert.IsNotNull(result.ErrorMessage);
    }

    [TestMethod]
    public void Terminate_InvalidProcessId_Throws()
    {
        var terminator = new GameProcessTerminator(new FakeProcessTerminatorBackend(closeMainWindowResult: true));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => terminator.Terminate(0));
    }

    private sealed class FakeProcessTerminatorBackend : IProcessTerminatorBackend
    {
        private readonly bool _closeMainWindowResult;
        private readonly bool _throwOnKill;

        public FakeProcessTerminatorBackend(bool closeMainWindowResult, bool throwOnKill = false)
        {
            _closeMainWindowResult = closeMainWindowResult;
            _throwOnKill = throwOnKill;
        }

        public int CloseAttempts { get; private set; }

        public int KillAttempts { get; private set; }

        public bool TryCloseMainWindow(int processId)
        {
            CloseAttempts++;
            return _closeMainWindowResult;
        }

        public void KillProcessTree(int processId)
        {
            KillAttempts++;
            if (_throwOnKill)
            {
                throw new InvalidOperationException("Kill failed.");
            }
        }
    }
}

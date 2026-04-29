namespace SteamWatch.Core.Tracking;

public sealed class RunningGameSession
{
    private TimeSpan _uncommitted;
    private bool _completed;

    public RunningGameSession(int appId, int processId, string gameName, DateTimeOffset startedAt)
    {
        if (appId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(appId), "AppId must be positive.");
        }

        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), "ProcessId must be positive.");
        }

        AppId = appId;
        ProcessId = processId;
        GameName = string.IsNullOrWhiteSpace(gameName) ? $"App {appId}" : gameName;
        StartedAt = startedAt;
        LastCheckpointAt = startedAt;
    }

    public int AppId { get; }

    public int ProcessId { get; }

    public string GameName { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset LastCheckpointAt { get; private set; }

    public bool IsCompleted => _completed;

    public PlaytimeIncrement Checkpoint(DateTimeOffset now)
    {
        EnsureActive();
        EnsureNotBeforeLastCheckpoint(now);

        _uncommitted += now - LastCheckpointAt;
        LastCheckpointAt = now;

        var minutes = (int)_uncommitted.TotalMinutes;
        if (minutes <= 0)
        {
            return new PlaytimeIncrement(AppId, GameName, 0, false);
        }

        _uncommitted -= TimeSpan.FromMinutes(minutes);
        return new PlaytimeIncrement(AppId, GameName, minutes, false);
    }

    public PlaytimeIncrement Complete(DateTimeOffset stoppedAt)
    {
        EnsureActive();

        var increment = Checkpoint(stoppedAt);
        var finalMinutes = increment.Minutes;

        if (_uncommitted > TimeSpan.Zero)
        {
            finalMinutes += 1;
            _uncommitted = TimeSpan.Zero;
        }

        _completed = true;
        return new PlaytimeIncrement(AppId, GameName, finalMinutes, true);
    }

    private void EnsureActive()
    {
        if (_completed)
        {
            throw new InvalidOperationException("Session is already completed.");
        }
    }

    private void EnsureNotBeforeLastCheckpoint(DateTimeOffset now)
    {
        if (now < LastCheckpointAt)
        {
            throw new ArgumentOutOfRangeException(nameof(now), "Checkpoint time cannot move backwards.");
        }
    }
}

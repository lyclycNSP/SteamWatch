namespace SteamWatch.App.Presentation;

/// <summary>UI-thread coordinator: reject duplicate commands and suspend new work during exit.</summary>
public sealed class UiOperationCoordinator
{
    public bool IsBusy { get; private set; }
    public bool IsSuspended { get; private set; }

    public async Task<bool> TryRunAsync(Func<Task> action)
    {
        if (IsBusy || IsSuspended) return false;
        IsBusy = true;
        try { await action(); return true; }
        finally { IsBusy = false; }
    }

    public bool TrySuspend()
    {
        if (IsBusy || IsSuspended) return false;
        IsSuspended = true;
        return true;
    }

    public void Resume() => IsSuspended = false;
}

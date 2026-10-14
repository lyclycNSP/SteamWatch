namespace SteamWatch.App.Presentation;

/// <summary>Serializes preparation until the caller exits or explicitly resumes.</summary>
public sealed class ExitPreparation
{
    public bool IsActive { get; private set; }

    public async Task<bool> TryPrepareAsync(Func<Task<bool>> confirmDraft,
        Func<Task<bool>> authorize, Func<Task> shutdown, Action resume,
        CancellationToken cancellationToken = default)
    {
        if (IsActive) return false;
        IsActive = true;
        var prepared = false;
        var shutdownStarted = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await confirmDraft()) return false;
            cancellationToken.ThrowIfCancellationRequested();
            // Saving the draft may have changed the required password or enabled protection.
            if (!await authorize()) return false;
            cancellationToken.ThrowIfCancellationRequested();
            shutdownStarted = true;
            await shutdown();
            cancellationToken.ThrowIfCancellationRequested();
            prepared = true;
            return true;
        }
        finally
        {
            if (!prepared)
            {
                try { if (shutdownStarted) resume(); }
                finally { IsActive = false; }
            }
        }
    }

    public void Reset() => IsActive = false;
}

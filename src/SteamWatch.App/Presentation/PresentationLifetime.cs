namespace SteamWatch.App.Presentation;

/// <summary>UI-thread attachment generation; queued work must belong to the current attachment.</summary>
public sealed class PresentationLifetime
{
    public bool IsAttached { get; private set; }
    public int Generation { get; private set; }

    public bool Attach()
    {
        if (IsAttached) return false;
        Generation++;
        IsAttached = true;
        return true;
    }

    public bool Detach()
    {
        if (!IsAttached) return false;
        IsAttached = false;
        return true;
    }

    public bool IsCurrent(int generation) => IsAttached && Generation == generation;
}

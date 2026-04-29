namespace SteamWatch.App.Services;

public sealed record GameStatusSnapshot(
    string StatusText,
    IReadOnlyList<GameRowViewModel> Games);

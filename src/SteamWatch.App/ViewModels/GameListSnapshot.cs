namespace SteamWatch.App;

public sealed record GameListSnapshot(
    string SteamPath,
    string? UserId,
    string StatusText,
    IReadOnlyList<GameRowViewModel> Games);

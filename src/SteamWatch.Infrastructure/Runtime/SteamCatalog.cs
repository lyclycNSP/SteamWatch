using SteamWatch.Core.Steam;
using SteamWatch.Infrastructure.Steam;

namespace SteamWatch.Infrastructure.Runtime;

public sealed record SteamCatalog(string SteamPath, string? UserId, IReadOnlyList<SteamGameInfo> Games);

public interface ISteamCatalogProvider
{
    Task<SteamCatalog> LoadAsync(CancellationToken cancellationToken = default);
}

public sealed class SteamCatalogProvider : ISteamCatalogProvider
{
    public async Task<SteamCatalog> LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = new SteamPathResolver().Resolve();
        var reader = new SteamCacheReader(path);
        var user = await reader.GetCurrentUserIdAsync(cancellationToken).ConfigureAwait(false);
        return new(path, user, await reader.ReadGamesAsync(user, cancellationToken).ConfigureAwait(false));
    }
}

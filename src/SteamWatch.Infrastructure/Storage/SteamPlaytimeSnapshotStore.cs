using SteamWatch.Core.Tracking;

namespace SteamWatch.Infrastructure.Storage;

public sealed class SteamPlaytimeSnapshotStore
{
    private const string StorageKey = "steam-playtime-snapshot";

    private readonly JsonFileStore _store;

    public SteamPlaytimeSnapshotStore(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<SteamPlaytimeSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        return await _store.LoadAsync<SteamPlaytimeSnapshot>(StorageKey, cancellationToken).ConfigureAwait(false);
    }

    public Task SaveAsync(SteamPlaytimeSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        return _store.SaveAsync(StorageKey, snapshot, cancellationToken);
    }
}

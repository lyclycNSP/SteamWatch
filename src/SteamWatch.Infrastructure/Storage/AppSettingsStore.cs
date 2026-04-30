using SteamWatch.Core.Settings;

namespace SteamWatch.Infrastructure.Storage;

public sealed class AppSettingsStore
{
    private const string StorageKey = "settings";

    private readonly JsonFileStore _store;

    public AppSettingsStore(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        return await _store.LoadAsync<AppSettings>(StorageKey, cancellationToken).ConfigureAwait(false)
            ?? new AppSettings();
    }

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        return _store.SaveAsync(StorageKey, settings, cancellationToken);
    }
}

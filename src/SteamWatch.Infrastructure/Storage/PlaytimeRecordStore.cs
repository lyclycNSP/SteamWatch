using SteamWatch.Core.Tracking;

namespace SteamWatch.Infrastructure.Storage;

public sealed class PlaytimeRecordStore
{
    private const string StorageKey = "playtime";

    private readonly JsonFileStore _store;

    public PlaytimeRecordStore(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<PlaytimeRecordBook> LoadAsync(CancellationToken cancellationToken = default)
    {
        var records = await _store.LoadAsync<List<DailyPlaytimeRecord>>(StorageKey, cancellationToken).ConfigureAwait(false);
        return new PlaytimeRecordBook(records);
    }

    public Task SaveAsync(PlaytimeRecordBook recordBook, CancellationToken cancellationToken = default)
    {
        return _store.SaveAsync(StorageKey, recordBook.Records, cancellationToken);
    }
}

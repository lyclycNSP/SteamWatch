using SteamWatch.Core.Tracking;

namespace SteamWatch.Infrastructure.Storage;

public sealed class PlaytimeLedgerStore(JsonFileStore store)
{
    public async Task<PlaytimeLedger> LoadAsync(CancellationToken cancellationToken = default)
    {
        var state = await store.LoadAsync<PlaytimeLedgerState>("playtime-ledger", cancellationToken).ConfigureAwait(false);
        if (state is not null)
        {
            var loaded = new PlaytimeLedger(state);
            if (state.SchemaVersion < PlaytimeLedger.CurrentSchemaVersion)
            {
                await store.SaveAsync($"playtime-ledger-before-v2-{Guid.NewGuid():N}", state, cancellationToken).ConfigureAwait(false);
                await SaveAsync(loaded, cancellationToken).ConfigureAwait(false);
            }
            return loaded;
        }
        var ledger = new PlaytimeLedger();
        var legacy = await new PlaytimeRecordStore(store).LoadAsync(cancellationToken).ConfigureAwait(false);
        ledger.ImportLegacy(legacy.Records);
        // Legacy Steam snapshots have no account identity; safely rebase on first observation.
        await SaveAsync(ledger, cancellationToken).ConfigureAwait(false);
        return ledger;
    }

    public Task SaveAsync(PlaytimeLedger ledger, CancellationToken cancellationToken = default) =>
        store.SaveAsync("playtime-ledger", ledger.Export(), cancellationToken);
}

using SteamWatch.Core.Limits;

namespace SteamWatch.Infrastructure.Storage;

public sealed class LimitRuleStore
{
    private const string StorageKey = "limits";

    private readonly JsonFileStore _store;

    public LimitRuleStore(JsonFileStore store)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<LimitRule>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var rules = await _store.LoadAsync<List<LimitRule>>(StorageKey, cancellationToken).ConfigureAwait(false);
        return rules ?? [];
    }

    public Task SaveAsync(IReadOnlyList<LimitRule> rules, CancellationToken cancellationToken = default)
    {
        return _store.SaveAsync(StorageKey, rules, cancellationToken);
    }
}

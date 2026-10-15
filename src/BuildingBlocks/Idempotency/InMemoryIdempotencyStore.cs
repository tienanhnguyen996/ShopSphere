using System.Collections.Concurrent;

namespace ShopSphere.BuildingBlocks.Idempotency;

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, IdempotencyRecord> _store = new();

    public Task<IdempotencyRecord?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var record))
        {
            if (record.ExpiresAtUtc > DateTime.UtcNow)
            {
                return Task.FromResult<IdempotencyRecord?>(record);
            }

            _store.TryRemove(key, out _);
        }

        return Task.FromResult<IdempotencyRecord?>(null);
    }

    public Task SaveAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
    {
        _store[record.Key] = record;
        return Task.CompletedTask;
    }
}

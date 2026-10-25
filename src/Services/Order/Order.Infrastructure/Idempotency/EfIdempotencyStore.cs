using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Idempotency;
using ShopSphere.Services.Order.Application.Common.Interfaces;

namespace ShopSphere.Services.Order.Infrastructure.Idempotency;

/// <summary>
/// Persistent SQL Server implementation of IIdempotencyStore for Order checkout operations.
/// </summary>
public class EfIdempotencyStore : IIdempotencyStore
{
    private readonly IOrderDbContext _dbContext;

    public EfIdempotencyStore(IOrderDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IdempotencyRecord?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalizedKey = key.Trim();
        var record = await _dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.Key == normalizedKey, cancellationToken);

        if (record is null)
        {
            return null;
        }

        if (record.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return null;
        }

        return record;
    }

    public async Task SaveAsync(IdempotencyRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var existing = await _dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.Key == record.Key, cancellationToken);

        if (existing is null)
        {
            _dbContext.IdempotencyRecords.Add(record);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

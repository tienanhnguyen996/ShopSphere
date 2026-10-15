using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.Services.Catalog.Domain;

namespace ShopSphere.Services.Catalog.Application.Common.Interfaces;

public interface ICatalogDbContext
{
    DbSet<Product> Products { get; }
    DbSet<PriceHistory> PriceHistories { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

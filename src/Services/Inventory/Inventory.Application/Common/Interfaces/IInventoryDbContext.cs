using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.Services.Inventory.Domain;

namespace ShopSphere.Services.Inventory.Application.Common.Interfaces;

public interface IInventoryDbContext
{
    DbSet<StockItem> StockItems { get; }
    DbSet<StockReservation> Reservations { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

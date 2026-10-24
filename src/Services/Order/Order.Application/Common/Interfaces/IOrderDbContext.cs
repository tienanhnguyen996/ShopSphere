using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Idempotency;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.Services.Order.Application.Saga;
using ShopSphere.Services.Order.Domain;

namespace ShopSphere.Services.Order.Application.Common.Interfaces;

public interface IOrderDbContext
{
    DbSet<ShopSphere.Services.Order.Domain.Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<CheckoutSagaState> CheckoutSagaStates { get; }
    DbSet<IdempotencyRecord> IdempotencyRecords { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

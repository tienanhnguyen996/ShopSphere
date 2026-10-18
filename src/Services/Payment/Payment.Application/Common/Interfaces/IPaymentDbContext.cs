using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.Services.Payment.Domain;

namespace ShopSphere.Services.Payment.Application.Common.Interfaces;

public interface IPaymentDbContext
{
    DbSet<PaymentAttempt> PaymentAttempts { get; }
    DbSet<PaymentWebhookLog> WebhookLogs { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

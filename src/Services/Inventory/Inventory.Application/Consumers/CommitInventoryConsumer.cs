using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Inventory.Application.Common.Interfaces;
using ShopSphere.Services.Inventory.Application.Contracts;
using ShopSphere.Services.Inventory.Domain;
using ShopSphere.Services.Inventory.Domain.Exceptions;

namespace ShopSphere.Services.Inventory.Application.Consumers;

public class CommitInventoryConsumer :
    IConsumer<CommitInventory>,
    IConsumer<MessageEnvelope<CommitInventory>>
{
    private readonly IInventoryDbContext _dbContext;
    private readonly ILogger<CommitInventoryConsumer> _logger;

    public CommitInventoryConsumer(
        IInventoryDbContext dbContext,
        ILogger<CommitInventoryConsumer> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<CommitInventory> context)
    {
        await HandleCommitAsync(context.Message, context, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<MessageEnvelope<CommitInventory>> context)
    {
        await HandleCommitAsync(context.Message.Payload, context, context.CancellationToken);
    }

    public async Task HandleCommitAsync(
        CommitInventory command,
        IPublishEndpoint publishEndpoint,
        CancellationToken ct = default)
    {
        var orderId = command.OrderId;

        var reservations = await _dbContext.Reservations
            .Where(r => r.OrderId == orderId)
            .ToListAsync(ct);

        if (reservations.Count == 0)
        {
            _logger.LogWarning("No reservations found to commit for Order {OrderId}", orderId);
            await PublishCommitFailedAsync(orderId, "No reservations found for order.", publishEndpoint, ct);
            return;
        }

        // Idempotency: if all reservations are already committed, republish success immediately
        if (reservations.All(r => r.Status == ReservationStatus.Committed))
        {
            _logger.LogInformation("Reservations for Order {OrderId} are already committed. Idempotent return.", orderId);
            var alreadyCommittedItems = reservations
                .Select(r => new ReservedItemDto(r.Sku, r.Quantity))
                .ToList();
            await PublishCommittedAsync(orderId, alreadyCommittedItems, publishEndpoint, ct);
            return;
        }

        var distinctSkus = reservations.Select(r => r.Sku).Distinct().ToList();
        var stockItems = await _dbContext.StockItems
            .Include(s => s.Reservations)
            .Where(s => distinctSkus.Contains(s.Sku))
            .ToListAsync(ct);

        var utcNow = DateTime.UtcNow;

        // Inside the commit transaction, verify reservation is still in Held state and ExpiresAtUtc > UtcNow
        try
        {
            foreach (var reservation in reservations)
            {
                var stockItem = stockItems.FirstOrDefault(s => s.Id == reservation.StockItemId)
                    ?? throw new InvalidOperationException($"Stock item for SKU '{reservation.Sku}' not found.");

                stockItem.CommitHold(reservation.Id, utcNow);
            }

            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully committed inventory for Order {OrderId}", orderId);

            var committedItems = reservations
                .Select(r => new ReservedItemDto(r.Sku, r.Quantity))
                .ToList();

            await PublishCommittedAsync(orderId, committedItems, publishEndpoint, ct);
        }
        catch (ReservationExpiredDomainException ex)
        {
            _logger.LogWarning(ex, "Reservation {ReservationId} for Order {OrderId} expired before commit",
                ex.ReservationId, orderId);

            // Save the state transition of expired hold and released stock
            await _dbContext.SaveChangesAsync(ct);

            await PublishCommitFailedAsync(
                orderId,
                $"Reservation expired at {ex.ExpiredAtUtc:O} UTC before payment was confirmed.",
                publishEndpoint,
                ct);
        }
    }

    private static async Task PublishCommittedAsync(
        Guid orderId,
        IReadOnlyList<ReservedItemDto> items,
        IPublishEndpoint publishEndpoint,
        CancellationToken ct)
    {
        var envelope = MessageEnvelope<InventoryCommitted>.Create(
            new InventoryCommitted(orderId, items),
            orderId.ToString());

        await publishEndpoint.Publish(envelope, ct);
        await publishEndpoint.Publish(envelope.Payload, ct);
    }

    private static async Task PublishCommitFailedAsync(
        Guid orderId,
        string reason,
        IPublishEndpoint publishEndpoint,
        CancellationToken ct)
    {
        var envelope = MessageEnvelope<InventoryCommitFailed>.Create(
            new InventoryCommitFailed(orderId, reason),
            orderId.ToString());

        await publishEndpoint.Publish(envelope, ct);
        await publishEndpoint.Publish(envelope.Payload, ct);
    }
}

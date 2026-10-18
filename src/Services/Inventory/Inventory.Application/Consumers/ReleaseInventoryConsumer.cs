using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Inventory.Application.Common.Interfaces;
using ShopSphere.Services.Inventory.Application.Contracts;
using ShopSphere.Services.Inventory.Domain;

namespace ShopSphere.Services.Inventory.Application.Consumers;

public class ReleaseInventoryConsumer :
    IConsumer<ReleaseInventory>,
    IConsumer<MessageEnvelope<ReleaseInventory>>
{
    private readonly IInventoryDbContext _dbContext;
    private readonly ILogger<ReleaseInventoryConsumer> _logger;

    public ReleaseInventoryConsumer(
        IInventoryDbContext dbContext,
        ILogger<ReleaseInventoryConsumer> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<ReleaseInventory> context)
    {
        await HandleReleaseAsync(context.Message, context, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<MessageEnvelope<ReleaseInventory>> context)
    {
        await HandleReleaseAsync(context.Message.Payload, context, context.CancellationToken);
    }

    public async Task HandleReleaseAsync(
        ReleaseInventory command,
        IPublishEndpoint publishEndpoint,
        CancellationToken ct = default)
    {
        var orderId = command.OrderId;
        var reason = command.Reason ?? "Order cancelled or payment failed";

        var reservations = await _dbContext.Reservations
            .Include(r => r.StockItem)
            .Where(r => r.OrderId == orderId)
            .ToListAsync(ct);

        if (reservations.Count == 0)
        {
            _logger.LogInformation("No reservations found to release for Order {OrderId}. Idempotent return.", orderId);
            await PublishReleasedEventAsync(orderId, reason, publishEndpoint, ct);
            return;
        }

        var skus = reservations.Select(r => r.Sku).Distinct().ToList();
        var stockItems = await _dbContext.StockItems
            .Include(s => s.Reservations)
            .Where(s => skus.Contains(s.Sku))
            .ToListAsync(ct);

        foreach (var reservation in reservations)
        {
            if (reservation.Status == ReservationStatus.Held)
            {
                var stockItem = stockItems.FirstOrDefault(s => s.Id == reservation.StockItemId)
                    ?? reservation.StockItem;

                stockItem?.ReleaseHold(reservation.Id, reason);
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Released inventory reservations for Order {OrderId}. Reason: {Reason}", orderId, reason);

        await PublishReleasedEventAsync(orderId, reason, publishEndpoint, ct);
    }

    private static async Task PublishReleasedEventAsync(
        Guid orderId,
        string reason,
        IPublishEndpoint publishEndpoint,
        CancellationToken ct)
    {
        var envelope = MessageEnvelope<InventoryReleased>.Create(
            new InventoryReleased(orderId, reason),
            orderId.ToString());

        await publishEndpoint.Publish(envelope, ct);
        await publishEndpoint.Publish(envelope.Payload, ct);
    }
}

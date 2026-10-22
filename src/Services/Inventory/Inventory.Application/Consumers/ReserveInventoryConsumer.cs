using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Inventory.Application.Common.Interfaces;
using ShopSphere.Services.Inventory.Application.Contracts;
using ShopSphere.Services.Inventory.Domain;

namespace ShopSphere.Services.Inventory.Application.Consumers;

public class ReserveInventoryConsumer :
    IConsumer<ReserveInventory>,
    IConsumer<MessageEnvelope<ReserveInventory>>
{
    private readonly IInventoryDbContext _dbContext;
    private readonly ILogger<ReserveInventoryConsumer> _logger;

    public ReserveInventoryConsumer(
        IInventoryDbContext dbContext,
        ILogger<ReserveInventoryConsumer> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Consume(ConsumeContext<ReserveInventory> context)
    {
        await HandleReservationAsync(context.Message, context, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<MessageEnvelope<ReserveInventory>> context)
    {
        await HandleReservationAsync(context.Message.Payload, context, context.CancellationToken);
    }

    public async Task HandleReservationAsync(
        ReserveInventory command,
        IPublishEndpoint publishEndpoint,
        CancellationToken ct = default)
    {
        var orderId = command.OrderId;

        // 1. Idempotency check: check if reservations already exist for this order
        var existingReservations = await _dbContext.Reservations
            .Where(r => r.OrderId == orderId)
            .ToListAsync(ct);

        if (existingReservations.Count > 0)
        {
            var activeHolds = existingReservations
                .Where(r => r.Status is ReservationStatus.Held or ReservationStatus.Committed)
                .ToList();

            if (activeHolds.Count > 0)
            {
                _logger.LogInformation("Duplicate ReserveInventory detected for Order {OrderId}. Returning existing reservation.", orderId);

                var reservedItems = activeHolds
                    .Select(r => new ReservedItemDto(r.Sku, r.Quantity))
                    .ToList();

                var maxExpiry = activeHolds.Max(r => r.ExpiresAtUtc);

                var reservedEnvelope = MessageEnvelope<InventoryReserved>.Create(
                    new InventoryReserved(orderId, reservedItems, maxExpiry),
                    orderId.ToString());

                await publishEndpoint.Publish(reservedEnvelope, ct);
                await publishEndpoint.Publish(reservedEnvelope.Payload, ct);
                return;
            }
        }

        // 2. Perform hold with optimistic concurrency retries (up to 3 attempts)
        const int maxRetries = 3;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var distinctSkus = command.Items
                    .Select(i => i.Sku.Trim().ToUpperInvariant())
                    .Distinct()
                    .ToList();

                var stockItems = await _dbContext.StockItems
                    .Include(s => s.Reservations)
                    .Where(s => distinctSkus.Contains(s.Sku))
                    .ToListAsync(ct);

                // Check for unavailable stock or missing SKUs
                var unavailableSkus = new List<string>();
                foreach (var item in command.Items)
                {
                    var normalizedSku = item.Sku.Trim().ToUpperInvariant();
                    var stockItem = stockItems.FirstOrDefault(s => s.Sku == normalizedSku);

                    if (stockItem is null || stockItem.QuantityAvailable < item.Quantity)
                    {
                        unavailableSkus.Add(normalizedSku);
                    }
                }

                if (unavailableSkus.Count > 0)
                {
                    _logger.LogWarning("Insufficient stock for Order {OrderId}. Unavailable SKUs: {Skus}",
                        orderId, string.Join(", ", unavailableSkus));

                    var rejectedEnvelope = MessageEnvelope<InventoryRejected>.Create(
                        new InventoryRejected(
                            orderId,
                            $"Insufficient stock for SKUs: {string.Join(", ", unavailableSkus)}",
                            unavailableSkus),
                        orderId.ToString());

                    await publishEndpoint.Publish(rejectedEnvelope, ct);
                    await publishEndpoint.Publish(rejectedEnvelope.Payload, ct);
                    await _dbContext.SaveChangesAsync(ct);
                    return;
                }

                // All stock items available - apply holds
                var ttl = command.ReservationTtl > TimeSpan.Zero ? command.ReservationTtl : TimeSpan.FromMinutes(15);
                var expiresAtUtc = DateTime.UtcNow.Add(ttl);
                var reservedDtoList = new List<ReservedItemDto>();

                foreach (var item in command.Items)
                {
                    var normalizedSku = item.Sku.Trim().ToUpperInvariant();
                    var stockItem = stockItems.First(s => s.Sku == normalizedSku);
                    stockItem.HoldStock(orderId, item.Quantity, ttl);
                    reservedDtoList.Add(new ReservedItemDto(normalizedSku, item.Quantity));
                }

                var successEnvelope = MessageEnvelope<InventoryReserved>.Create(
                    new InventoryReserved(orderId, reservedDtoList, expiresAtUtc),
                    orderId.ToString());

                await _dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("Successfully reserved inventory for Order {OrderId}", orderId);

                await publishEndpoint.Publish(successEnvelope, ct);
                await publishEndpoint.Publish(successEnvelope.Payload, ct);
                return;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict on reservation attempt {Attempt} for Order {OrderId}",
                    attempt, orderId);

                if (attempt == maxRetries)
                {
                    _logger.LogError("Exhausted concurrency retries for Order {OrderId}. Rejecting reservation.", orderId);
                    var allSkus = command.Items.Select(i => i.Sku).ToList();
                    var rejectedEnvelope = MessageEnvelope<InventoryRejected>.Create(
                        new InventoryRejected(orderId, "Concurrency conflict during stock reservation.", allSkus),
                        orderId.ToString());

                    await _dbContext.SaveChangesAsync(ct);

                    await publishEndpoint.Publish(rejectedEnvelope, ct);
                    await publishEndpoint.Publish(rejectedEnvelope.Payload, ct);
                    return;
                }

                // Short backoff before reloading fresh state
                await Task.Delay(50 * attempt, ct);
            }
        }
    }
}

using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Inventory.Application.Common.Interfaces;
using ShopSphere.Services.Inventory.Domain;
using ShopSphere.Services.Shipping.Application.Contracts;

namespace ShopSphere.Services.Inventory.Application.Consumers;

public sealed class ShipmentHandedOverConsumer(
    IInventoryDbContext dbContext,
    ILogger<ShipmentHandedOverConsumer> logger) :
    IConsumer<ShipmentHandedOver>,
    IConsumer<MessageEnvelope<ShipmentHandedOver>>
{
    public Task Consume(ConsumeContext<ShipmentHandedOver> context) =>
        HandleShipmentHandedOverAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<MessageEnvelope<ShipmentHandedOver>> context) =>
        HandleShipmentHandedOverAsync(context.Message.Payload, context.CancellationToken);

    public async Task HandleShipmentHandedOverAsync(
        ShipmentHandedOver evt,
        CancellationToken ct = default)
    {
        var reservations = await dbContext.Reservations
            .Where(r => r.OrderId == evt.OrderId)
            .ToListAsync(ct);

        if (reservations.Count == 0)
        {
            logger.LogWarning(
                "No inventory reservations found for Order {OrderId} on ShipmentHandedOver {ShipmentId}",
                evt.OrderId,
                evt.ShipmentId);
            return;
        }

        if (reservations.All(r => r.Status == ReservationStatus.Committed))
        {
            logger.LogInformation(
                "Inventory reservations for Order {OrderId} already committed at physical handover of Shipment {ShipmentId} ({TrackingNumber})",
                evt.OrderId,
                evt.ShipmentId,
                evt.TrackingNumber);
            return;
        }

        var distinctSkus = reservations.Select(r => r.Sku).Distinct().ToList();
        var stockItems = await dbContext.StockItems
            .Include(s => s.Reservations)
            .Where(s => distinctSkus.Contains(s.Sku))
            .ToListAsync(ct);

        foreach (var reservation in reservations.Where(r => r.Status == ReservationStatus.Held))
        {
            var stockItem = stockItems.FirstOrDefault(s => s.Id == reservation.StockItemId);
            stockItem?.CommitHold(reservation.Id, evt.HandedOverAtUtc);
        }

        await dbContext.SaveChangesAsync(ct);
        logger.LogInformation(
            "Finalized inventory physical handover for Order {OrderId} / Shipment {ShipmentId}",
            evt.OrderId,
            evt.ShipmentId);
    }
}

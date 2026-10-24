using MassTransit;
using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Shipping.Application.Abstractions;
using ShopSphere.Services.Shipping.Application.Contracts;
using ShopSphere.Services.Shipping.Application.DTOs;
using ShopSphere.Services.Shipping.Domain.Exceptions;

namespace ShopSphere.Services.Shipping.Application.Commands;

public sealed record RecordDeliveryCommand(
    Guid ShipmentId,
    string Location,
    string Notes,
    string? CorrelationId = null);

public sealed class RecordDeliveryCommandHandler(
    IShippingDbContext dbContext,
    IPublishEndpoint publishEndpoint)
{
    public async Task<ShipmentDto> HandleAsync(
        RecordDeliveryCommand command,
        CancellationToken cancellationToken = default)
    {
        var shipment = await dbContext.Shipments
            .Include(s => s.Items)
            .Include(s => s.Milestones)
            .FirstOrDefaultAsync(s => s.Id == command.ShipmentId, cancellationToken)
            ?? throw new ShipmentNotFoundException(command.ShipmentId);

        var previousStatus = shipment.Status;
        shipment.RecordDelivered(command.Location);

        if (previousStatus != shipment.Status)
        {
            var correlationId = Guid.TryParse(command.CorrelationId, out var parsed)
                ? parsed
                : shipment.OrderId;

            var evt = new ShipmentDelivered(
                shipment.OrderId,
                shipment.Id,
                shipment.Carrier,
                shipment.TrackingNumber ?? string.Empty,
                shipment.DeliveredAtUtc ?? DateTime.UtcNow);

            var envelope = MessageEnvelope<ShipmentDelivered>.Create(
                evt,
                shipment.OrderId.ToString(),
                correlationId);

            await publishEndpoint.Publish(envelope, cancellationToken);
            await publishEndpoint.Publish(evt, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ShipmentDto.FromDomain(shipment);
    }
}

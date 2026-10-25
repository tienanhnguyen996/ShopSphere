using MassTransit;
using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Shipping.Application.Abstractions;
using ShopSphere.Services.Shipping.Application.Contracts;
using ShopSphere.Services.Shipping.Application.DTOs;
using ShopSphere.Services.Shipping.Domain.Exceptions;

namespace ShopSphere.Services.Shipping.Application.Commands;

public sealed record RecordCarrierHandoverCommand(
    Guid ShipmentId,
    string WarehouseLocation,
    string Notes,
    string? CorrelationId = null);

public sealed class RecordCarrierHandoverCommandHandler(
    IShippingDbContext dbContext,
    IPublishEndpoint publishEndpoint)
{
    public async Task<ShipmentDto> HandleAsync(
        RecordCarrierHandoverCommand command,
        CancellationToken cancellationToken = default)
    {
        var shipment = await dbContext.Shipments
            .Include(s => s.Items)
            .Include(s => s.Milestones)
            .FirstOrDefaultAsync(s => s.Id == command.ShipmentId, cancellationToken)
            ?? throw new ShipmentNotFoundException(command.ShipmentId);

        var previousStatus = shipment.Status;
        shipment.RecordCarrierHandover(command.WarehouseLocation);

        if (previousStatus != shipment.Status)
        {
            var correlationId = Guid.TryParse(command.CorrelationId, out var parsed)
                ? parsed
                : shipment.OrderId;

            var evt = new ShipmentHandedOver(
                shipment.OrderId,
                shipment.Id,
                shipment.Carrier,
                shipment.TrackingNumber ?? string.Empty,
                shipment.HandedOverAtUtc ?? DateTime.UtcNow);

            var envelope = MessageEnvelope<ShipmentHandedOver>.Create(
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

using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Shipping.Application.Abstractions;
using ShopSphere.Services.Shipping.Application.Contracts;
using ShopSphere.Services.Shipping.Domain;

namespace ShopSphere.Services.Shipping.Application.Consumers;

public sealed class CreateShipmentConsumer(
    IShippingDbContext dbContext,
    ICarrierProviderClient carrierProviderClient,
    ILogger<CreateShipmentConsumer> logger)
    : IConsumer<CreateShipment>, IConsumer<MessageEnvelope<CreateShipment>>
{
    public Task Consume(ConsumeContext<MessageEnvelope<CreateShipment>> context) =>
        HandleCreateShipmentAsync(
            context.Message.Payload,
            context.Message.CorrelationId,
            context.Message.MessageId,
            context,
            context.CancellationToken);

    public Task Consume(ConsumeContext<CreateShipment> context) =>
        HandleCreateShipmentAsync(
            context.Message,
            context.CorrelationId ?? context.Message.OrderId,
            context.MessageId ?? Guid.NewGuid(),
            context,
            context.CancellationToken);

    private async Task HandleCreateShipmentAsync(
        CreateShipment command,
        Guid correlationId,
        Guid causationId,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Shipments
            .Include(s => s.Items)
            .Include(s => s.Milestones)
            .FirstOrDefaultAsync(s => s.OrderId == command.OrderId, cancellationToken);

        if (existing is not null)
        {
            if (existing.Status is ShipmentStatus.LabelCreated or ShipmentStatus.HandedOver or ShipmentStatus.InTransit or ShipmentStatus.Delivered)
            {
                logger.LogInformation(
                    "Idempotent CreateShipment handled for OrderId {OrderId}; returning existing Shipment {ShipmentId} ({TrackingNumber})",
                    command.OrderId,
                    existing.Id,
                    existing.TrackingNumber);

                await PublishShipmentCreatedAsync(publishEndpoint, existing, correlationId, causationId, cancellationToken);
                return;
            }

            if (existing.Status == ShipmentStatus.ShipmentUnknown && !string.IsNullOrWhiteSpace(existing.ExternalShipmentRequestId))
            {
                logger.LogInformation(
                    "Shipment {ShipmentId} for OrderId {OrderId} is in ShipmentUnknown state; querying carrier status for ExternalShipmentRequestId {ExternalShipmentRequestId}",
                    existing.Id,
                    command.OrderId,
                    existing.ExternalShipmentRequestId);

                var statusResult = await carrierProviderClient.QueryLabelStatusAsync(existing.ExternalShipmentRequestId, cancellationToken);
                await ApplyCarrierResultAsync(existing, statusResult, publishEndpoint, correlationId, causationId, cancellationToken);
                return;
            }
        }

        var shipment = existing ?? CreateNewShipment(command);
        if (existing is null)
        {
            dbContext.Shipments.Add(shipment);
        }

        var carrierRequestId = shipment.ExternalShipmentRequestId ?? $"SHP-REQ-{command.OrderId:N}";
        var result = await carrierProviderClient.RequestLabelAsync(
            command.OrderId,
            carrierRequestId,
            shipment.Address,
            shipment.Items,
            cancellationToken);

        await ApplyCarrierResultAsync(shipment, result, publishEndpoint, correlationId, causationId, cancellationToken);
    }

    private static Shipment CreateNewShipment(CreateShipment command)
    {
        var shipmentId = Guid.NewGuid();
        var address = ShippingAddress.Create(
            command.Address.RecipientName,
            command.Address.Street,
            command.Address.City,
            command.Address.State,
            command.Address.PostalCode,
            command.Address.Country,
            command.Address.Phone);

        var items = command.Items
            .Select(i => ShipmentItem.Create(shipmentId, i.Sku, i.ProductName, i.Quantity))
            .ToList();

        return Shipment.Create(shipmentId, command.OrderId, command.CustomerId, address, items);
    }

    private async Task ApplyCarrierResultAsync(
        Shipment shipment,
        CarrierLabelResult result,
        IPublishEndpoint publishEndpoint,
        Guid correlationId,
        Guid causationId,
        CancellationToken cancellationToken)
    {
        switch (result.Status)
        {
            case CarrierLabelStatus.Created:
                shipment.AssignTrackingLabel(
                    result.TrackingNumber ?? $"TRK-{shipment.OrderId:N}"[..16].ToUpperInvariant(),
                    result.CarrierRequestId);

                await PublishShipmentCreatedAsync(publishEndpoint, shipment, correlationId, causationId, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                break;

            case CarrierLabelStatus.UnknownTimeout:
                // Rule: Timeout = Unknown, not automatic failure. Store ExternalShipmentRequestId and leave in ShipmentUnknown.
                shipment.MarkUnknown(
                    result.CarrierRequestId,
                    result.ErrorReason ?? "Carrier label request timed out; outcome unknown.");
                await dbContext.SaveChangesAsync(cancellationToken);
                break;

            default:
                var reason = result.ErrorReason ?? "Carrier rejected shipment label creation.";
                shipment.MarkFailed(reason);
                await PublishShipmentFailedAsync(publishEndpoint, shipment.OrderId, reason, correlationId, causationId, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                break;
        }
    }

    private static async Task PublishShipmentCreatedAsync(
        IPublishEndpoint publishEndpoint,
        Shipment shipment,
        Guid correlationId,
        Guid causationId,
        CancellationToken cancellationToken)
    {
        var evt = new ShipmentCreated(
            shipment.OrderId,
            shipment.Id,
            shipment.TrackingNumber ?? string.Empty);

        var envelope = MessageEnvelope<ShipmentCreated>.Create(
            evt,
            shipment.OrderId.ToString(),
            correlationId,
            causationId);

        await publishEndpoint.Publish(envelope, cancellationToken);
        await publishEndpoint.Publish(evt, cancellationToken);
    }

    private static async Task PublishShipmentFailedAsync(
        IPublishEndpoint publishEndpoint,
        Guid orderId,
        string reason,
        Guid correlationId,
        Guid causationId,
        CancellationToken cancellationToken)
    {
        var evt = new ShipmentFailed(orderId, reason);
        var envelope = MessageEnvelope<ShipmentFailed>.Create(
            evt,
            orderId.ToString(),
            correlationId,
            causationId);

        await publishEndpoint.Publish(envelope, cancellationToken);
        await publishEndpoint.Publish(evt, cancellationToken);
    }
}

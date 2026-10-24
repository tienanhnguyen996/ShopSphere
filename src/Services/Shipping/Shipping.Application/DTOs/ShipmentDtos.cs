using ShopSphere.Services.Shipping.Domain;

namespace ShopSphere.Services.Shipping.Application.DTOs;

public sealed record ShipmentAddressResponseDto(
    string RecipientName,
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country,
    string Phone);

public sealed record ShipmentItemResponseDto(
    Guid Id,
    string Sku,
    string ProductName,
    int Quantity);

public sealed record TrackingMilestoneResponseDto(
    Guid Id,
    string Status,
    string? Location,
    string Description,
    DateTime OccurredAtUtc);

public sealed record ShipmentDto(
    Guid Id,
    Guid OrderId,
    string CustomerId,
    string Status,
    string Carrier,
    string? TrackingNumber,
    string? ExternalShipmentRequestId,
    ShipmentAddressResponseDto Address,
    string? FailureReason,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? HandedOverAtUtc,
    DateTime? DeliveredAtUtc,
    IReadOnlyList<ShipmentItemResponseDto> Items,
    IReadOnlyList<TrackingMilestoneResponseDto> Milestones)
{
    public static ShipmentDto FromDomain(Shipment shipment) =>
        new(
            shipment.Id,
            shipment.OrderId,
            shipment.CustomerId,
            shipment.Status.ToString(),
            shipment.Carrier,
            shipment.TrackingNumber,
            shipment.ExternalShipmentRequestId,
            new ShipmentAddressResponseDto(
                shipment.Address.RecipientName,
                shipment.Address.Street,
                shipment.Address.City,
                shipment.Address.State,
                shipment.Address.PostalCode,
                shipment.Address.Country,
                shipment.Address.Phone),
            shipment.FailureReason,
            shipment.CreatedAtUtc,
            shipment.UpdatedAtUtc,
            shipment.HandedOverAtUtc,
            shipment.DeliveredAtUtc,
            shipment.Items
                .Select(i => new ShipmentItemResponseDto(i.Id, i.Sku, i.ProductName, i.Quantity))
                .ToList(),
            shipment.Milestones
                .OrderBy(m => m.OccurredAtUtc)
                .Select(m => new TrackingMilestoneResponseDto(m.Id, m.Status.ToString(), m.Location, m.Description, m.OccurredAtUtc))
                .ToList());
}

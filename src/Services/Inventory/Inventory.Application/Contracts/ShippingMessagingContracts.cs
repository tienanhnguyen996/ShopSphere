namespace ShopSphere.Services.Shipping.Application.Contracts;

public sealed record ShipmentHandedOver(
    Guid OrderId,
    Guid ShipmentId,
    string Carrier,
    string TrackingNumber,
    DateTime HandedOverAtUtc);

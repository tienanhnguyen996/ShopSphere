using ShopSphere.BuildingBlocks.Common.Domain;

namespace ShopSphere.Services.Shipping.Domain.Events;

public sealed record ShipmentCreatedDomainEvent(
    Guid ShipmentId,
    Guid OrderId,
    string CustomerId,
    string TrackingNumber) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}

public sealed record ShipmentHandedOverDomainEvent(
    Guid ShipmentId,
    Guid OrderId,
    string TrackingNumber,
    DateTime HandedOverAtUtc) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}

public sealed record ShipmentDeliveredDomainEvent(
    Guid ShipmentId,
    Guid OrderId,
    string TrackingNumber,
    DateTime DeliveredAtUtc) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}

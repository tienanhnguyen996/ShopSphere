using ShopSphere.BuildingBlocks.Common.Domain;

namespace ShopSphere.Services.Order.Domain.Events;

public sealed record OrderCreatedDomainEvent(
    Guid OrderId,
    string CustomerId,
    decimal TotalAmount,
    string Currency,
    string PaymentMethod) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}

public sealed record OrderStatusChangedDomainEvent(
    Guid OrderId,
    OrderStatus PreviousStatus,
    OrderStatus NewStatus,
    string? Reason = null) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}

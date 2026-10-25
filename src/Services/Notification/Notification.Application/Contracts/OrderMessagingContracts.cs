namespace ShopSphere.Services.Order.Application.Contracts;

public record OrderConfirmed(
    Guid OrderId,
    string CustomerId,
    decimal TotalAmount,
    string Currency,
    Guid ShipmentId,
    string TrackingNumber);

public record OrderCancelled(
    Guid OrderId,
    string CustomerId,
    string Reason);

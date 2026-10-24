using ShopSphere.Services.Shipping.Application.Contracts;

namespace ShopSphere.Services.Order.Application.Contracts;

public record OrderItemSnapshotDto(
    string Sku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string Currency,
    int PriceVersion);

public record OrderSubmitted(
    Guid OrderId,
    string CustomerId,
    decimal TotalAmount,
    string Currency,
    string PaymentMethod,
    ShippingAddressDto ShippingAddress,
    IReadOnlyList<OrderItemSnapshotDto> Items);

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

using ShopSphere.Services.Order.Domain;

namespace ShopSphere.Services.Order.Application.Orders.Dtos;

public record ShippingAddressRequest(
    string RecipientName,
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country,
    string Phone);

public record CheckoutRequest(
    ShippingAddressRequest ShippingAddress,
    string PaymentMethod);

public record CancelOrderRequest(string Reason);

public record OrderItemDto(
    Guid Id,
    string Sku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string Currency,
    int PriceVersion);

public record OrderDto(
    Guid Id,
    string CustomerId,
    string Status,
    decimal TotalAmount,
    string Currency,
    string PaymentMethod,
    ShippingAddressRequest ShippingAddress,
    IReadOnlyCollection<OrderItemDto> Items,
    Guid? PaymentAttemptId,
    Guid? ShipmentId,
    string? TrackingNumber,
    string? FailureReason,
    string? CancellationReason,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public record PagedOrderResult(
    IReadOnlyCollection<OrderDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public static class OrderMappingExtensions
{
    public static OrderDto ToDto(this ShopSphere.Services.Order.Domain.Order order)
    {
        var items = order.Items
            .Select(i => new OrderItemDto(
                i.Id,
                i.Sku,
                i.ProductName,
                i.Quantity,
                i.UnitPrice,
                i.LineTotal,
                i.Currency,
                i.PriceVersion))
            .ToList();

        var address = new ShippingAddressRequest(
            order.ShippingAddress.RecipientName,
            order.ShippingAddress.Street,
            order.ShippingAddress.City,
            order.ShippingAddress.State,
            order.ShippingAddress.PostalCode,
            order.ShippingAddress.Country,
            order.ShippingAddress.Phone);

        return new OrderDto(
            order.Id,
            order.CustomerId,
            order.Status.ToString(),
            order.TotalAmount,
            order.Currency,
            order.PaymentMethod,
            address,
            items,
            order.PaymentAttemptId,
            order.ShipmentId,
            order.TrackingNumber,
            order.FailureReason,
            order.CancellationReason,
            order.CreatedAtUtc,
            order.UpdatedAtUtc);
    }
}

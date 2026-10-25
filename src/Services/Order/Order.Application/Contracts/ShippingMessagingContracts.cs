namespace ShopSphere.Services.Shipping.Application.Contracts;

public record ShippingAddressDto(
    string RecipientName,
    string Street,
    string City,
    string State,
    string PostalCode,
    string Country,
    string Phone);

public record ShipmentItemDto(
    string Sku,
    string ProductName,
    int Quantity);

public record CreateShipment(
    Guid OrderId,
    string CustomerId,
    ShippingAddressDto Address,
    IReadOnlyList<ShipmentItemDto> Items);

public record ShipmentCreated(
    Guid OrderId,
    Guid ShipmentId,
    string TrackingNumber);

public record ShipmentFailed(
    Guid OrderId,
    string Reason);

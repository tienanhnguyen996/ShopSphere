namespace ShopSphere.Services.Cart.Application.Carts.Dtos;

public record CartItemDto(
    string Sku,
    int Quantity);

public record CartDto(
    string CustomerId,
    IReadOnlyCollection<CartItemDto> Items,
    int TotalQuantity,
    DateTime CreatedAtUtc,
    DateTime LastModifiedUtc);

public record CheckoutQuoteItemDto(
    string Sku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string Currency,
    int PriceVersion,
    bool IsActive);

public record CheckoutQuoteDto(
    string CustomerId,
    IReadOnlyCollection<CheckoutQuoteItemDto> Items,
    decimal Subtotal,
    string Currency,
    IReadOnlyDictionary<string, int> PriceVersions,
    bool IsValid,
    IReadOnlyCollection<string> ValidationErrors);

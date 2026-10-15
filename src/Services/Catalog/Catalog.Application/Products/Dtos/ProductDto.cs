namespace ShopSphere.Services.Catalog.Application.Products.Dtos;

public sealed record ProductDto(
    Guid Id,
    string Name,
    string Description,
    string Sku,
    decimal Price,
    string Currency,
    string Status,
    int PriceVersion,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

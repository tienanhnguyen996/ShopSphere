namespace ShopSphere.Services.Catalog.Application.Products.Commands.UpdatePrice;

public sealed record UpdateProductPriceCommand(
    string Sku,
    decimal NewPrice,
    string? Currency = null);

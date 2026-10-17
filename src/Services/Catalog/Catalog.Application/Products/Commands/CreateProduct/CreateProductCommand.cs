namespace ShopSphere.Services.Catalog.Application.Products.Commands.CreateProduct;

public sealed record CreateProductCommand(
    string Name,
    string Description,
    string Sku,
    decimal PriceAmount,
    string Currency = "USD",
    bool ActivateImmediately = true);

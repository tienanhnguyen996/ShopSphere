using ShopSphere.BuildingBlocks.Common.Results;

namespace ShopSphere.Services.Cart.Application.Common.Interfaces;

public record CatalogProductDto(
    Guid Id,
    string Name,
    string Description,
    string Sku,
    decimal Price,
    string Currency,
    string Status,
    int PriceVersion);

public interface ICatalogClient
{
    Task<Result<CatalogProductDto>> GetProductBySkuAsync(string sku, CancellationToken ct = default);
}

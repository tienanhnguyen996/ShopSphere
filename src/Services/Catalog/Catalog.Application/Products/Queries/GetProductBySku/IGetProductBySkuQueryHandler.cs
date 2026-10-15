using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Catalog.Application.Products.Dtos;

namespace ShopSphere.Services.Catalog.Application.Products.Queries.GetProductBySku;

public interface IGetProductBySkuQueryHandler
{
    Task<Result<ProductDto>> HandleAsync(GetProductBySkuQuery query, CancellationToken ct = default);
}

using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Catalog.Application.Common.Models;
using ShopSphere.Services.Catalog.Application.Products.Dtos;

namespace ShopSphere.Services.Catalog.Application.Products.Queries.GetProducts;

public interface IGetProductsQueryHandler
{
    Task<Result<PagedResult<ProductDto>>> HandleAsync(GetProductsQuery query, CancellationToken ct = default);
}

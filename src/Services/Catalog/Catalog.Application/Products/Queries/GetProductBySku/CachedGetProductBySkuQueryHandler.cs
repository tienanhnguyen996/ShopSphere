using Microsoft.Extensions.Logging;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Catalog.Application.Common.Interfaces;
using ShopSphere.Services.Catalog.Application.Products.Dtos;

namespace ShopSphere.Services.Catalog.Application.Products.Queries.GetProductBySku;

public sealed class CachedGetProductBySkuQueryHandler : IGetProductBySkuQueryHandler
{
    private readonly IGetProductBySkuQueryHandler _inner;
    private readonly ICacheService _cacheService;
    private readonly ILogger<CachedGetProductBySkuQueryHandler> _logger;

    public CachedGetProductBySkuQueryHandler(
        IGetProductBySkuQueryHandler inner,
        ICacheService cacheService,
        ILogger<CachedGetProductBySkuQueryHandler> logger)
    {
        _inner = inner;
        _cacheService = cacheService;
        _logger = logger;
    }

    public static string GetCacheKey(string sku) => $"catalog:product:{sku?.Trim().ToUpperInvariant()}";

    public async Task<Result<ProductDto>> HandleAsync(GetProductBySkuQuery query, CancellationToken ct = default)
    {
        var cacheKey = GetCacheKey(query.Sku);

        var cached = await _cacheService.GetAsync<ProductDto>(cacheKey, ct);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for product SKU '{Sku}'.", query.Sku);
            return Result.Success(cached);
        }

        _logger.LogDebug("Cache miss for product SKU '{Sku}'. Fetching from database.", query.Sku);
        var result = await _inner.HandleAsync(query, ct);

        if (result.IsSuccess)
        {
            await _cacheService.SetAsync(cacheKey, result.Value, TimeSpan.FromMinutes(30), ct);
        }

        return result;
    }
}

using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Inventory.Application.Common.Interfaces;
using ShopSphere.Services.Inventory.Application.Stock.Dtos;

namespace ShopSphere.Services.Inventory.Application.Stock.Queries.GetStockBySku;

public interface IGetStockBySkuQueryHandler
{
    Task<Result<StockDto>> HandleAsync(GetStockBySkuQuery query, CancellationToken ct = default);
}

public class GetStockBySkuQueryHandler : IGetStockBySkuQueryHandler
{
    private readonly IInventoryDbContext _dbContext;

    public GetStockBySkuQueryHandler(IInventoryDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<Result<StockDto>> HandleAsync(GetStockBySkuQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Sku))
        {
            return Result.Failure<StockDto>(
                Error.Validation("Inventory.InvalidSku", "SKU is required."));
        }

        var normalizedSku = query.Sku.Trim().ToUpperInvariant();
        var stockItem = await _dbContext.StockItems
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Sku == normalizedSku, ct);

        if (stockItem is null)
        {
            return Result.Failure<StockDto>(
                Error.NotFound("Inventory.NotFound", $"Stock item with SKU '{query.Sku}' was not found."));
        }

        var dto = new StockDto(
            stockItem.Id,
            stockItem.Sku,
            stockItem.QuantityAvailable,
            stockItem.QuantityHeld,
            stockItem.QuantityCommitted,
            stockItem.QuantityAvailable + stockItem.QuantityHeld + stockItem.QuantityCommitted,
            stockItem.UpdatedAtUtc ?? stockItem.CreatedAtUtc);

        return Result.Success(dto);
    }
}

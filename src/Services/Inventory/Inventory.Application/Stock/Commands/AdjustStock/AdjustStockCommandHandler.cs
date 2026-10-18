using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Inventory.Application.Common.Interfaces;
using ShopSphere.Services.Inventory.Application.Stock.Dtos;
using ShopSphere.Services.Inventory.Domain;

namespace ShopSphere.Services.Inventory.Application.Stock.Commands.AdjustStock;

public interface IAdjustStockCommandHandler
{
    Task<Result<StockDto>> HandleAsync(AdjustStockCommand command, CancellationToken ct = default);
}

public class AdjustStockCommandHandler : IAdjustStockCommandHandler
{
    private readonly IInventoryDbContext _dbContext;
    private readonly IValidator<AdjustStockCommand> _validator;
    private readonly ILogger<AdjustStockCommandHandler> _logger;

    public AdjustStockCommandHandler(
        IInventoryDbContext dbContext,
        IValidator<AdjustStockCommand> validator,
        ILogger<AdjustStockCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<StockDto>> HandleAsync(AdjustStockCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var validationResult = await _validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            var firstError = validationResult.Errors[0];
            return Result.Failure<StockDto>(
                Error.Validation(firstError.PropertyName, firstError.ErrorMessage));
        }

        var normalizedSku = command.Sku.Trim().ToUpperInvariant();
        var stockItem = await _dbContext.StockItems
            .FirstOrDefaultAsync(s => s.Sku == normalizedSku, ct);

        if (stockItem is null)
        {
            if (command.Quantity < 0)
            {
                return Result.Failure<StockDto>(
                    Error.NotFound("Inventory.NotFound", $"Stock item with SKU '{command.Sku}' was not found."));
            }

            // Positive intake creates new stock item if not yet registered
            stockItem = StockItem.Create(normalizedSku, 0);
            _dbContext.StockItems.Add(stockItem);
        }

        try
        {
            stockItem.AdjustStock(command.Quantity, command.Reason ?? string.Empty);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<StockDto>(
                Error.Validation("Inventory.NegativeAvailableStock", ex.Message));
        }

        _logger.LogInformation(
            "Stock adjustment applied for SKU '{Sku}': Delta {Delta}, Available now {Available}, Reason: '{Reason}'",
            stockItem.Sku, command.Quantity, stockItem.QuantityAvailable, command.Reason);

        await _dbContext.SaveChangesAsync(ct);

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

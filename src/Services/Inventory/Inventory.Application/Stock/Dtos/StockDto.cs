namespace ShopSphere.Services.Inventory.Application.Stock.Dtos;

/// <summary>
/// Data transfer object representing the real-time stock allocation for a SKU.
/// Never cached in Redis; queried directly from SQL Server.
/// </summary>
public record StockDto(
    Guid Id,
    string Sku,
    int QuantityAvailable,
    int QuantityHeld,
    int QuantityCommitted,
    int TotalStock,
    DateTime? LastModifiedUtc);

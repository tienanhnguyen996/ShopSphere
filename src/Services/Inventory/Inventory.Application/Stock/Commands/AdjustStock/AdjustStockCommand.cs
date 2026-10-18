namespace ShopSphere.Services.Inventory.Application.Stock.Commands.AdjustStock;

public record AdjustStockCommand(
    string Sku,
    int Quantity,
    string? Reason);

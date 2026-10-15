namespace ShopSphere.Services.Inventory.Domain.Exceptions;

/// <summary>
/// Domain exception thrown when stock cannot be reserved due to insufficient available quantity.
/// </summary>
public class InsufficientStockDomainException : Exception
{
    public string Sku { get; }
    public int RequestedQuantity { get; }
    public int AvailableQuantity { get; }

    public InsufficientStockDomainException(string sku, int requestedQuantity, int availableQuantity)
        : base($"Insufficient available stock for SKU '{sku}'. Requested: {requestedQuantity}, Available: {availableQuantity}.")
    {
        Sku = sku;
        RequestedQuantity = requestedQuantity;
        AvailableQuantity = availableQuantity;
    }
}

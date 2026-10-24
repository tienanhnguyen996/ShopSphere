using ShopSphere.BuildingBlocks.Common.Domain;
using ShopSphere.Services.Order.Domain.Exceptions;

namespace ShopSphere.Services.Order.Domain;

/// <summary>
/// Immutable line item snapshot captured at order submission time.
/// Preserves the exact unit price and catalog price version verified during checkout.
/// </summary>
public class OrderItem : Entity<Guid>
{
    public Guid OrderId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public int PriceVersion { get; private set; }

    private OrderItem() { }

    private OrderItem(
        Guid id,
        Guid orderId,
        string sku,
        string productName,
        int quantity,
        decimal unitPrice,
        string currency,
        int priceVersion) : base(id)
    {
        if (orderId == Guid.Empty)
        {
            throw new OrderDomainException("OrderId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new OrderDomainException("SKU is required for order item.");
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new OrderDomainException("Product name is required for order item.");
        }

        if (quantity <= 0)
        {
            throw new OrderDomainException("Quantity must be greater than zero.");
        }

        if (unitPrice < 0)
        {
            throw new OrderDomainException("Unit price cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw new OrderDomainException("Currency must be a valid 3-character ISO code.");
        }

        if (priceVersion <= 0)
        {
            throw new OrderDomainException("Price version must be positive.");
        }

        OrderId = orderId;
        Sku = sku.Trim().ToUpperInvariant();
        ProductName = productName.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineTotal = decimal.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero);
        Currency = currency.Trim().ToUpperInvariant();
        PriceVersion = priceVersion;
    }

    public static OrderItem Create(
        Guid orderId,
        string sku,
        string productName,
        int quantity,
        decimal unitPrice,
        string currency,
        int priceVersion)
    {
        return new OrderItem(
            Guid.NewGuid(),
            orderId,
            sku,
            productName,
            quantity,
            unitPrice,
            currency,
            priceVersion);
    }
}

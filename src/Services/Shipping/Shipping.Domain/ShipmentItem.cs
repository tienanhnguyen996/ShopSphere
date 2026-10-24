using ShopSphere.BuildingBlocks.Common.Domain;
using ShopSphere.Services.Shipping.Domain.Exceptions;

namespace ShopSphere.Services.Shipping.Domain;

public class ShipmentItem : Entity<Guid>
{
    public Guid ShipmentId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }

    private ShipmentItem() { }

    private ShipmentItem(Guid id, Guid shipmentId, string sku, string productName, int quantity)
        : base(id)
    {
        if (shipmentId == Guid.Empty)
        {
            throw new ShippingDomainException("ShipmentId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ShippingDomainException("SKU is required for shipment item.");
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new ShippingDomainException("Product name is required for shipment item.");
        }

        if (quantity <= 0)
        {
            throw new ShippingDomainException("Quantity must be greater than zero.");
        }

        ShipmentId = shipmentId;
        Sku = sku.Trim().ToUpperInvariant();
        ProductName = productName.Trim();
        Quantity = quantity;
    }

    public static ShipmentItem Create(Guid shipmentId, string sku, string productName, int quantity)
    {
        return new ShipmentItem(Guid.NewGuid(), shipmentId, sku, productName, quantity);
    }
}

using ShopSphere.BuildingBlocks.Common.Domain;
using ShopSphere.Services.Catalog.Domain.Events;

namespace ShopSphere.Services.Catalog.Domain;

public sealed class Product : AggregateRoot<Guid>, IVersionedEntity, IAuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Sku Sku { get; private set; } = default!;
    public Money Price { get; private set; } = default!;
    public ProductStatus Status { get; private set; } = ProductStatus.Draft;
    public int PriceVersion { get; private set; } = 1;

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    private Product() { }

    private Product(Guid id, string name, string description, Sku sku, Money price) : base(id)
    {
        SetName(name);
        SetDescription(description);
        Sku = sku ?? throw new ArgumentNullException(nameof(sku));
        Price = price ?? throw new ArgumentNullException(nameof(price));
        Status = ProductStatus.Draft;
        PriceVersion = 1;
    }

    public static Product Create(Guid id, string name, string description, Sku sku, Money price)
    {
        return new Product(id, name, description, sku, price);
    }

    public void UpdateDetails(string name, string description)
    {
        if (Status == ProductStatus.Discontinued)
        {
            throw new InvalidOperationException("Cannot update details of a discontinued product.");
        }

        SetName(name);
        SetDescription(description);
    }

    public void UpdatePrice(Money newPrice)
    {
        ArgumentNullException.ThrowIfNull(newPrice);

        if (Status == ProductStatus.Discontinued)
        {
            throw new InvalidOperationException("Cannot change price of a discontinued product.");
        }

        if (Price.Equals(newPrice))
        {
            return;
        }

        var oldPrice = Price;
        Price = newPrice;
        PriceVersion++;

        AddDomainEvent(new ProductPriceChangedDomainEvent(Id, Sku, oldPrice, newPrice, PriceVersion));
    }

    public void Activate()
    {
        if (Status == ProductStatus.Discontinued)
        {
            throw new InvalidOperationException("Discontinued products cannot be reactivated.");
        }

        Status = ProductStatus.Active;
    }

    public void Discontinue()
    {
        Status = ProductStatus.Discontinued;
    }

    private void SetName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name cannot be empty.", nameof(name));
        }

        Name = name.Trim();
    }

    private void SetDescription(string description)
    {
        Description = description?.Trim() ?? string.Empty;
    }
}

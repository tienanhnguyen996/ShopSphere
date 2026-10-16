namespace ShopSphere.Services.Catalog.Domain;

public sealed class PriceHistory
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public decimal OldPriceAmount { get; private set; }
    public decimal NewPriceAmount { get; private set; }
    public string Currency { get; private set; } = "USD";
    public int PriceVersion { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }

    private PriceHistory() { }

    public PriceHistory(
        Guid id,
        Guid productId,
        string sku,
        decimal oldPriceAmount,
        decimal newPriceAmount,
        string currency,
        int priceVersion,
        DateTime changedAtUtc)
    {
        Id = id;
        ProductId = productId;
        Sku = sku;
        OldPriceAmount = oldPriceAmount;
        NewPriceAmount = newPriceAmount;
        Currency = currency;
        PriceVersion = priceVersion;
        ChangedAtUtc = changedAtUtc;
    }
}

using ShopSphere.BuildingBlocks.Common.Domain;

namespace ShopSphere.Services.Catalog.Domain;

public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money()
    {
        Amount = 0m;
        Currency = "USD";
    }

    public Money(decimal amount, string currency = "USD")
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw new ArgumentException("Currency must be a valid 3-letter ISO code.", nameof(currency));
        }

        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        Currency = currency.Trim().ToUpperInvariant();
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Amount;
        yield return Currency;
    }

    public static Money Zero(string currency = "USD") => new(0m, currency);

    public override string ToString() => $"{Amount:0.00} {Currency}";
}

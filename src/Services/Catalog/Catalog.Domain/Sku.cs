using System.Text.RegularExpressions;
using ShopSphere.BuildingBlocks.Common.Domain;

namespace ShopSphere.Services.Catalog.Domain;

public sealed class Sku : ValueObject
{
    private static readonly Regex SkuPattern = new(@"^[A-Z0-9-_]+$", RegexOptions.Compiled);

    public string Value { get; }

    private Sku() => Value = string.Empty;

    public Sku(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("SKU cannot be null or whitespace.", nameof(value));
        }

        var trimmed = value.Trim().ToUpperInvariant();
        if (trimmed.Length is < 3 or > 50)
        {
            throw new ArgumentException("SKU length must be between 3 and 50 characters.", nameof(value));
        }

        if (!SkuPattern.IsMatch(trimmed))
        {
            throw new ArgumentException("SKU must be alphanumeric (letters, numbers, hyphens, and underscores).", nameof(value));
        }

        Value = trimmed;
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(Sku sku) => sku.Value;
    public static explicit operator Sku(string value) => new(value);
}

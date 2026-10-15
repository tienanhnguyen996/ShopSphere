using System.Text.RegularExpressions;
using ShopSphere.Services.Cart.Domain.Exceptions;

namespace ShopSphere.Services.Cart.Domain;

public record CartItem
{
    private static readonly Regex _skuRegex = new("^[A-Z0-9-_]+$", RegexOptions.Compiled);

    public string Sku { get; init; }
    public int Quantity { get; init; }

    public CartItem(string sku, int quantity)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new DomainValidationException("CartItem.InvalidSku", "SKU cannot be empty.");
        }

        var normalizedSku = sku.Trim().ToUpperInvariant();
        if (!_skuRegex.IsMatch(normalizedSku))
        {
            throw new DomainValidationException("CartItem.InvalidSku", $"SKU '{sku}' must contain only uppercase alphanumeric characters, dashes, or underscores.");
        }

        if (quantity < 1 || quantity > 99)
        {
            throw new DomainValidationException("CartItem.InvalidQuantity", $"Quantity must be between 1 and 99. Provided: {quantity}.");
        }

        Sku = normalizedSku;
        Quantity = quantity;
    }
}

using System.Text.Json.Serialization;
using ShopSphere.Services.Cart.Domain.Exceptions;

namespace ShopSphere.Services.Cart.Domain;

public class Cart
{
    private List<CartItem> _items = new();

    [JsonInclude]
    public string CustomerId { get; private set; } = string.Empty;

    [JsonInclude]
    public IReadOnlyCollection<CartItem> Items
    {
        get => _items.AsReadOnly();
        private init => _items = value != null ? new List<CartItem>(value) : new List<CartItem>();
    }

    [JsonInclude]
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    [JsonInclude]
    public DateTime LastModifiedUtc { get; private set; } = DateTime.UtcNow;

    public int TotalQuantity => _items.Sum(i => i.Quantity);

    [JsonConstructor]
    public Cart()
    {
    }

    public Cart(string customerId, IReadOnlyCollection<CartItem>? items = null, DateTime? createdAtUtc = null, DateTime? lastModifiedUtc = null)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new DomainValidationException("Cart.InvalidCustomerId", "Customer ID cannot be empty.");
        }

        CustomerId = customerId.Trim();
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
        LastModifiedUtc = lastModifiedUtc ?? CreatedAtUtc;

        if (items is not null)
        {
            _items.AddRange(items);
        }
    }

    public void AddItem(string sku, int quantity)
    {
        if (quantity < 1 || quantity > 99)
        {
            throw new DomainValidationException("Cart.InvalidQuantity", $"Quantity to add must be between 1 and 99. Provided: {quantity}.");
        }

        var normalizedSku = sku?.Trim().ToUpperInvariant()
            ?? throw new DomainValidationException("Cart.InvalidSku", "SKU cannot be null.");

        var existingIndex = _items.FindIndex(i => i.Sku == normalizedSku);
        if (existingIndex >= 0)
        {
            var newQuantity = _items[existingIndex].Quantity + quantity;
            if (newQuantity > 99)
            {
                throw new DomainValidationException("Cart.QuantityExceeded", $"Total quantity for SKU '{normalizedSku}' cannot exceed 99. Current: {_items[existingIndex].Quantity}, requested addition: {quantity}.");
            }

            _items[existingIndex] = new CartItem(normalizedSku, newQuantity);
        }
        else
        {
            _items.Add(new CartItem(normalizedSku, quantity));
        }

        LastModifiedUtc = DateTime.UtcNow;
    }

    public void UpdateItemQuantity(string sku, int quantity)
    {
        var normalizedSku = sku?.Trim().ToUpperInvariant()
            ?? throw new DomainValidationException("Cart.InvalidSku", "SKU cannot be null.");

        if (quantity <= 0)
        {
            RemoveItem(normalizedSku);
            return;
        }

        if (quantity > 99)
        {
            throw new DomainValidationException("Cart.QuantityExceeded", $"Quantity for SKU '{normalizedSku}' cannot exceed 99. Provided: {quantity}.");
        }

        var existingIndex = _items.FindIndex(i => i.Sku == normalizedSku);
        if (existingIndex >= 0)
        {
            _items[existingIndex] = new CartItem(normalizedSku, quantity);
        }
        else
        {
            _items.Add(new CartItem(normalizedSku, quantity));
        }

        LastModifiedUtc = DateTime.UtcNow;
    }

    public void RemoveItem(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return;
        }

        var normalizedSku = sku.Trim().ToUpperInvariant();
        _items.RemoveAll(i => i.Sku == normalizedSku);
        LastModifiedUtc = DateTime.UtcNow;
    }

    public void Clear()
    {
        _items.Clear();
        LastModifiedUtc = DateTime.UtcNow;
    }
}

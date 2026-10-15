using ShopSphere.BuildingBlocks.Common.Domain;
using ShopSphere.Services.Inventory.Domain.Exceptions;

namespace ShopSphere.Services.Inventory.Domain;

/// <summary>
/// Entity representing a held or committed stock allocation for an order.
/// </summary>
public class StockReservation : Entity<Guid>, IAuditableEntity
{
    public Guid StockItemId { get; private set; }
    public Guid OrderId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public ReservationStatus Status { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    // Navigation property for EF Core
    public StockItem? StockItem { get; private set; }

    // Parameterless constructor for EF Core
    private StockReservation() { }

    public StockReservation(
        Guid id,
        Guid stockItemId,
        Guid orderId,
        string sku,
        int quantity,
        ReservationStatus status,
        DateTime expiresAtUtc) : base(id)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("SKU cannot be empty.", nameof(sku));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Reserved quantity must be greater than zero.");
        }

        StockItemId = stockItemId;
        OrderId = orderId;
        Sku = sku.Trim().ToUpperInvariant();
        Quantity = quantity;
        Status = status;
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void MarkCommitted(DateTime utcNow)
    {
        if (Status == ReservationStatus.Committed)
        {
            return;
        }

        if (ExpiresAtUtc <= utcNow || Status == ReservationStatus.Expired)
        {
            Status = ReservationStatus.Expired;
            UpdatedAtUtc = utcNow;
            throw new ReservationExpiredDomainException(Id, OrderId, ExpiresAtUtc);
        }

        if (Status != ReservationStatus.Held)
        {
            throw new InvalidOperationException($"Cannot commit reservation '{Id}' from status '{Status}'.");
        }

        Status = ReservationStatus.Committed;
        UpdatedAtUtc = utcNow;
    }

    public void MarkReleased(string reason)
    {
        if (Status == ReservationStatus.Committed)
        {
            throw new InvalidOperationException($"Cannot release committed reservation '{Id}'.");
        }

        if (Status == ReservationStatus.Released || Status == ReservationStatus.Expired)
        {
            return;
        }

        Status = ReservationStatus.Released;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkExpired()
    {
        if (Status == ReservationStatus.Committed)
        {
            throw new InvalidOperationException($"Cannot expire committed reservation '{Id}'.");
        }

        Status = ReservationStatus.Expired;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

using ShopSphere.BuildingBlocks.Common.Domain;
using ShopSphere.Services.Inventory.Domain.Exceptions;

namespace ShopSphere.Services.Inventory.Domain;

/// <summary>
/// Aggregate root representing an inventory item and tracking physical/logical stock allocations.
/// Stock counts are NEVER cached in Redis and rely on SQL concurrency tokens (RowVersion).
/// </summary>
public class StockItem : AggregateRoot<Guid>, IVersionedEntity, IAuditableEntity
{
    private readonly List<StockReservation> _reservations = new();

    public string Sku { get; private set; } = string.Empty;
    public int QuantityAvailable { get; private set; }
    public int QuantityHeld { get; private set; }
    public int QuantityCommitted { get; private set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public IReadOnlyCollection<StockReservation> Reservations => _reservations.AsReadOnly();

    // Parameterless constructor for EF Core
    private StockItem() { }

    public StockItem(Guid id, string sku, int initialStock) : base(id)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("SKU cannot be empty.", nameof(sku));
        }

        if (initialStock < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialStock), "Initial stock cannot be negative.");
        }

        Sku = sku.Trim().ToUpperInvariant();
        QuantityAvailable = initialStock;
        QuantityHeld = 0;
        QuantityCommitted = 0;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static StockItem Create(string sku, int initialStock = 0)
    {
        return new StockItem(Guid.NewGuid(), sku, initialStock);
    }

    /// <summary>
    /// Holds stock for an order for a given duration. Decreases QuantityAvailable and increases QuantityHeld.
    /// </summary>
    public StockReservation HoldStock(Guid orderId, int quantity, TimeSpan ttl)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId cannot be empty.", nameof(orderId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Hold quantity must be greater than zero.");
        }

        if (QuantityAvailable < quantity)
        {
            throw new InsufficientStockDomainException(Sku, quantity, QuantityAvailable);
        }

        QuantityAvailable -= quantity;
        QuantityHeld += quantity;
        UpdatedAtUtc = DateTime.UtcNow;

        var expiresAtUtc = DateTime.UtcNow.Add(ttl);
        var reservation = new StockReservation(
            Guid.NewGuid(),
            Id,
            orderId,
            Sku,
            quantity,
            ReservationStatus.Held,
            expiresAtUtc);

        _reservations.Add(reservation);
        return reservation;
    }

    /// <summary>
    /// Commits a held reservation after successful payment. Decreases QuantityHeld and increases QuantityCommitted.
    /// Expiry check occurs inside the transaction.
    /// </summary>
    public StockReservation CommitHold(Guid reservationId, DateTime? utcNow = null)
    {
        var reservation = _reservations.FirstOrDefault(r => r.Id == reservationId)
            ?? throw new InvalidOperationException($"Reservation '{reservationId}' not found on stock item '{Sku}'.");

        if (reservation.Status == ReservationStatus.Committed)
        {
            return reservation;
        }

        var now = utcNow ?? DateTime.UtcNow;

        if (reservation.ExpiresAtUtc <= now || reservation.Status == ReservationStatus.Expired)
        {
            if (reservation.Status == ReservationStatus.Held)
            {
                QuantityHeld -= reservation.Quantity;
                QuantityAvailable += reservation.Quantity;
                UpdatedAtUtc = now;
            }

            reservation.MarkExpired();
            throw new ReservationExpiredDomainException(reservation.Id, reservation.OrderId, reservation.ExpiresAtUtc);
        }

        if (reservation.Status != ReservationStatus.Held)
        {
            throw new InvalidOperationException($"Cannot commit reservation '{reservationId}' in status '{reservation.Status}'.");
        }

        QuantityHeld -= reservation.Quantity;
        QuantityCommitted += reservation.Quantity;
        UpdatedAtUtc = now;

        reservation.MarkCommitted(now);
        return reservation;
    }

    /// <summary>
    /// Releases a held reservation upon payment rejection, cancellation, or compensation.
    /// Decreases QuantityHeld and increases QuantityAvailable.
    /// </summary>
    public StockReservation ReleaseHold(Guid reservationId, string reason = "")
    {
        var reservation = _reservations.FirstOrDefault(r => r.Id == reservationId)
            ?? throw new InvalidOperationException($"Reservation '{reservationId}' not found on stock item '{Sku}'.");

        if (reservation.Status == ReservationStatus.Released || reservation.Status == ReservationStatus.Expired)
        {
            return reservation;
        }

        if (reservation.Status == ReservationStatus.Committed)
        {
            throw new InvalidOperationException($"Cannot release committed reservation '{reservationId}'.");
        }

        QuantityHeld -= reservation.Quantity;
        QuantityAvailable += reservation.Quantity;
        UpdatedAtUtc = DateTime.UtcNow;

        reservation.MarkReleased(reason);
        return reservation;
    }

    /// <summary>
    /// Expire an overdue hold. Moves held quantity back to available.
    /// </summary>
    public StockReservation ExpireHold(Guid reservationId)
    {
        var reservation = _reservations.FirstOrDefault(r => r.Id == reservationId)
            ?? throw new InvalidOperationException($"Reservation '{reservationId}' not found on stock item '{Sku}'.");

        if (reservation.Status == ReservationStatus.Expired || reservation.Status == ReservationStatus.Released)
        {
            return reservation;
        }

        if (reservation.Status == ReservationStatus.Committed)
        {
            throw new InvalidOperationException($"Cannot expire committed reservation '{reservationId}'.");
        }

        QuantityHeld -= reservation.Quantity;
        QuantityAvailable += reservation.Quantity;
        UpdatedAtUtc = DateTime.UtcNow;

        reservation.MarkExpired();
        return reservation;
    }

    /// <summary>
    /// Adjusts available stock levels (intake/restock or shrinkage corrections).
    /// </summary>
    public void AdjustStock(int adjustmentQuantity, string reason = "")
    {
        if (QuantityAvailable + adjustmentQuantity < 0)
        {
            throw new InvalidOperationException(
                $"Negative stock adjustment '{adjustmentQuantity}' would exceed available quantity '{QuantityAvailable}' for SKU '{Sku}'.");
        }

        QuantityAvailable += adjustmentQuantity;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

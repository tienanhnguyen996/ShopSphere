namespace ShopSphere.Services.Inventory.Application.Contracts;

/// <summary>
/// Requirement for a single SKU in a reservation request.
/// </summary>
public record InventoryItemRequirement(string Sku, int Quantity);

/// <summary>
/// Command to reserve inventory for an order with a TTL.
/// </summary>
public record ReserveInventory(
    Guid OrderId,
    IReadOnlyList<InventoryItemRequirement> Items,
    TimeSpan ReservationTtl);

/// <summary>
/// DTO representing an item reserved for an order.
/// </summary>
public record ReservedItemDto(string Sku, int Quantity);

/// <summary>
/// Event emitted when all items for an order are successfully reserved.
/// </summary>
public record InventoryReserved(
    Guid OrderId,
    IReadOnlyList<ReservedItemDto> Items,
    DateTime ExpiresAtUtc);

/// <summary>
/// Event emitted when reservation fails due to insufficient stock or invalid items.
/// </summary>
public record InventoryRejected(
    Guid OrderId,
    string Reason,
    IReadOnlyList<string> UnavailableSkus);

/// <summary>
/// Command to release a held inventory reservation.
/// </summary>
public record ReleaseInventory(
    Guid OrderId,
    string Reason);

/// <summary>
/// Event emitted when a reservation is successfully released.
/// </summary>
public record InventoryReleased(
    Guid OrderId,
    string Reason);

/// <summary>
/// Command to commit inventory after payment succeeded.
/// </summary>
public record CommitInventory(Guid OrderId);

/// <summary>
/// Event emitted when held stock is committed to an order.
/// </summary>
public record InventoryCommitted(
    Guid OrderId,
    IReadOnlyList<ReservedItemDto> Items);

/// <summary>
/// Event emitted when commit fails (e.g. reservation expired).
/// </summary>
public record InventoryCommitFailed(
    Guid OrderId,
    string Reason);

/// <summary>
/// Event emitted by the background worker when an expired reservation hold is swept and released.
/// </summary>
public record InventoryReservationExpired(
    Guid ReservationId,
    Guid OrderId,
    string Sku,
    int Quantity,
    DateTime ExpiredAtUtc);

namespace ShopSphere.Services.Inventory.Application.Contracts;

public record InventoryItemRequirement(string Sku, int Quantity);

public record ReserveInventory(
    Guid OrderId,
    IReadOnlyList<InventoryItemRequirement> Items,
    TimeSpan ReservationTtl);

public record ReservedItemDto(string Sku, int Quantity);

public record InventoryReserved(
    Guid OrderId,
    IReadOnlyList<ReservedItemDto> Items,
    DateTime ExpiresAtUtc);

public record InventoryRejected(
    Guid OrderId,
    string Reason,
    IReadOnlyList<string> UnavailableSkus);

public record ReleaseInventory(
    Guid OrderId,
    string Reason);

public record InventoryReleased(
    Guid OrderId,
    string Reason);

public record CommitInventory(Guid OrderId);

public record InventoryCommitted(
    Guid OrderId,
    IReadOnlyList<ReservedItemDto> Items);

public record InventoryCommitFailed(
    Guid OrderId,
    string Reason);

public record InventoryReservationExpired(
    Guid ReservationId,
    Guid OrderId,
    string Sku,
    int Quantity,
    DateTime ExpiredAtUtc);

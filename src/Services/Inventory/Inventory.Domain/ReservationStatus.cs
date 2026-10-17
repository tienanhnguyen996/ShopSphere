namespace ShopSphere.Services.Inventory.Domain;

/// <summary>
/// Lifecycle status of an inventory stock reservation.
/// </summary>
public enum ReservationStatus
{
    Held = 1,
    Committed = 2,
    Released = 3,
    Expired = 4
}

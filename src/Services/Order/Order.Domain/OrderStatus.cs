namespace ShopSphere.Services.Order.Domain;

/// <summary>
/// Explicit states of an Order throughout the Checkout Saga lifecycle.
/// </summary>
public enum OrderStatus
{
    Pending = 0,
    Reserving = 1,
    Paying = 2,
    PaymentUnknown = 3,
    Confirmed = 4,
    Refunding = 5,
    Cancelled = 6
}

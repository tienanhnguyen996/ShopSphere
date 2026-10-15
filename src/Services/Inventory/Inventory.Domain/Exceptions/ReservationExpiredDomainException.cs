namespace ShopSphere.Services.Inventory.Domain.Exceptions;

/// <summary>
/// Domain exception thrown when an attempt is made to commit an expired reservation hold.
/// </summary>
public class ReservationExpiredDomainException : Exception
{
    public Guid ReservationId { get; }
    public Guid OrderId { get; }
    public DateTime ExpiredAtUtc { get; }

    public ReservationExpiredDomainException(Guid reservationId, Guid orderId, DateTime expiredAtUtc)
        : base($"Reservation '{reservationId}' for order '{orderId}' has expired at {expiredAtUtc:O} UTC.")
    {
        ReservationId = reservationId;
        OrderId = orderId;
        ExpiredAtUtc = expiredAtUtc;
    }
}

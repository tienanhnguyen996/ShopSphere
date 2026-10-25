using MassTransit;

namespace ShopSphere.Services.Order.Application.Saga;

/// <summary>
/// Persistent state instance for the MassTransit Checkout State Machine Saga.
/// Uses CorrelationId == OrderId.
/// </summary>
public class CheckoutSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string ShippingAddressJson { get; set; } = string.Empty;
    public string ItemsJson { get; set; } = string.Empty;
    public Guid? PaymentAttemptId { get; set; }
    public Guid? ShipmentId { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime? ReservationExpiresAtUtc { get; set; }
    public bool IsReservationExpired { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

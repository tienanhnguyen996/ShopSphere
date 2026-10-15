namespace ShopSphere.Services.Payment.Application.Contracts;

/// <summary>
/// Command to collect payment for an order.
/// </summary>
public record CollectPayment(
    Guid OrderId,
    decimal Amount,
    string Currency,
    string PaymentMethod);

/// <summary>
/// Event emitted when payment is confirmed succeeded by provider.
/// </summary>
public record PaymentSucceeded(
    Guid OrderId,
    Guid PaymentAttemptId,
    string ProviderTransactionId,
    decimal Amount,
    string Currency);

/// <summary>
/// Event emitted when payment is rejected/declined by provider.
/// </summary>
public record PaymentRejected(
    Guid OrderId,
    Guid PaymentAttemptId,
    string Reason);

/// <summary>
/// Event emitted when provider call times out or returns ambiguous status.
/// Triggers reconciliation or alerts; order stays in PaymentUnknown, NOT failed.
/// </summary>
public record PaymentPendingResolution(
    Guid OrderId,
    Guid PaymentAttemptId,
    string Reason);

/// <summary>
/// Command to refund a previously collected payment.
/// </summary>
public record RefundPayment(
    Guid OrderId,
    Guid PaymentAttemptId,
    decimal Amount,
    string Currency,
    string Reason);

/// <summary>
/// Event emitted when payment refund is completed.
/// </summary>
public record PaymentRefunded(
    Guid OrderId,
    Guid PaymentAttemptId,
    string RefundTransactionId,
    decimal Amount,
    string Currency);

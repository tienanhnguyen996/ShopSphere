namespace ShopSphere.Services.Payment.Application.Contracts;

public record CollectPayment(
    Guid OrderId,
    decimal Amount,
    string Currency,
    string PaymentMethod);

public record PaymentSucceeded(
    Guid OrderId,
    Guid PaymentAttemptId,
    string ProviderTransactionId,
    decimal Amount,
    string Currency);

public record PaymentRejected(
    Guid OrderId,
    Guid PaymentAttemptId,
    string Reason);

public record PaymentPendingResolution(
    Guid OrderId,
    Guid PaymentAttemptId,
    string Reason);

public record RefundPayment(
    Guid OrderId,
    Guid PaymentAttemptId,
    decimal Amount,
    string Currency,
    string Reason);

public record PaymentRefunded(
    Guid OrderId,
    Guid PaymentAttemptId,
    string RefundTransactionId,
    decimal Amount,
    string Currency);

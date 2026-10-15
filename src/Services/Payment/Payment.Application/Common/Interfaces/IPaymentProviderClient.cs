namespace ShopSphere.Services.Payment.Application.Common.Interfaces;

public enum PaymentProviderStatus
{
    Success = 1,
    Failed = 2,
    Unknown = 3
}

public record PaymentChargeRequest(
    Guid PaymentAttemptId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string PaymentMethod);

public record PaymentChargeResult(
    PaymentProviderStatus Status,
    string? ProviderTransactionId,
    string? ErrorMessage);

public record PaymentStatusQueryRequest(
    Guid PaymentAttemptId,
    string? ProviderTransactionId);

public record PaymentStatusQueryResult(
    PaymentProviderStatus Status,
    string? ProviderTransactionId,
    string? ErrorMessage);

public record PaymentRefundRequest(
    Guid PaymentAttemptId,
    string ProviderTransactionId,
    decimal Amount,
    string Currency,
    string Reason);

public record PaymentRefundResult(
    PaymentProviderStatus Status,
    string? RefundTransactionId,
    string? ErrorMessage);

public interface IPaymentProviderClient
{
    Task<PaymentChargeResult> ChargeAsync(PaymentChargeRequest request, CancellationToken cancellationToken = default);
    Task<PaymentStatusQueryResult> GetStatusAsync(PaymentStatusQueryRequest request, CancellationToken cancellationToken = default);
    Task<PaymentRefundResult> RefundAsync(PaymentRefundRequest request, CancellationToken cancellationToken = default);
}

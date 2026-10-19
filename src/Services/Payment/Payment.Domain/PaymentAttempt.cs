using ShopSphere.BuildingBlocks.Common.Domain;
using ShopSphere.Services.Payment.Domain.Events;
using ShopSphere.Services.Payment.Domain.Exceptions;

namespace ShopSphere.Services.Payment.Domain;

/// <summary>
/// Aggregate root representing an attempt to process a payment for an order.
/// Models explicit state for provider timeouts (PaymentUnknown) to prevent blind retries.
/// </summary>
public class PaymentAttempt : AggregateRoot<Guid>, IVersionedEntity, IAuditableEntity
{
    public Guid OrderId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string PaymentMethod { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }
    public string? ProviderTransactionId { get; private set; }
    public string? FailureReason { get; private set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    // Private constructor for EF Core materialization
    private PaymentAttempt() { }

    public PaymentAttempt(Guid id, Guid orderId, decimal amount, string currency, string paymentMethod)
        : base(id)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("OrderId cannot be empty.", nameof(orderId));
        }

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw new ArgumentException("Currency must be a valid 3-letter ISO code.", nameof(currency));
        }

        if (string.IsNullOrWhiteSpace(paymentMethod))
        {
            throw new ArgumentException("Payment method cannot be empty.", nameof(paymentMethod));
        }

        OrderId = orderId;
        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
        PaymentMethod = paymentMethod.Trim().ToLowerInvariant();
        Status = PaymentStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new PaymentAttemptCreatedDomainEvent(Id, OrderId, Amount, Currency, PaymentMethod));
    }

    public static PaymentAttempt Create(Guid orderId, decimal amount, string currency, string paymentMethod)
    {
        return new PaymentAttempt(Guid.NewGuid(), orderId, amount, currency, paymentMethod);
    }

    /// <summary>
    /// Marks the payment attempt as PaymentUnknown when the provider call times out
    /// or returns an ambiguous result. Does not treat timeout as failure!
    /// </summary>
    public void MarkUnknown(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason for unknown payment state must be provided.", nameof(reason));
        }

        if (Status == PaymentStatus.Succeeded || Status == PaymentStatus.Failed || Status == PaymentStatus.Refunded)
        {
            throw new InvalidPaymentStateTransitionException(
                Status,
                PaymentStatus.PaymentUnknown,
                $"Cannot transition from terminal state {Status} to {PaymentStatus.PaymentUnknown}.");
        }

        Status = PaymentStatus.PaymentUnknown;
        FailureReason = reason;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new PaymentAttemptUnknownDomainEvent(Id, OrderId, reason));
    }

    /// <summary>
    /// Marks the payment attempt as Succeeded once the provider confirms the transaction.
    /// Idempotent if already in Succeeded state with identical provider transaction.
    /// </summary>
    public void MarkSucceeded(string providerTransactionId)
    {
        if (string.IsNullOrWhiteSpace(providerTransactionId))
        {
            throw new ArgumentException("Provider transaction ID cannot be empty.", nameof(providerTransactionId));
        }

        if (Status == PaymentStatus.Succeeded)
        {
            // Idempotent: already succeeded
            return;
        }

        if (Status == PaymentStatus.Failed || Status == PaymentStatus.Refunded)
        {
            throw new InvalidPaymentStateTransitionException(
                Status,
                PaymentStatus.Succeeded,
                $"Cannot transition from terminal state {Status} to {PaymentStatus.Succeeded}.");
        }

        Status = PaymentStatus.Succeeded;
        ProviderTransactionId = providerTransactionId.Trim();
        FailureReason = null;
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new PaymentAttemptSucceededDomainEvent(Id, OrderId, ProviderTransactionId, Amount, Currency));
    }

    /// <summary>
    /// Marks the payment attempt as Failed when the provider explicitly declines or rejects the charge.
    /// Idempotent if already Failed.
    /// </summary>
    public void MarkFailed(string failureReason, string? providerTransactionId = null)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
        {
            throw new ArgumentException("Failure reason cannot be empty.", nameof(failureReason));
        }

        if (Status == PaymentStatus.Failed)
        {
            // Idempotent: already failed
            return;
        }

        if (Status == PaymentStatus.Succeeded || Status == PaymentStatus.Refunded)
        {
            throw new InvalidPaymentStateTransitionException(
                Status,
                PaymentStatus.Failed,
                $"Cannot transition from terminal state {Status} to {PaymentStatus.Failed}.");
        }

        Status = PaymentStatus.Failed;
        FailureReason = failureReason.Trim();
        if (!string.IsNullOrWhiteSpace(providerTransactionId))
        {
            ProviderTransactionId = providerTransactionId.Trim();
        }
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new PaymentAttemptFailedDomainEvent(Id, OrderId, FailureReason, ProviderTransactionId));
    }

    /// <summary>
    /// Processes a refund for a previously succeeded payment attempt.
    /// </summary>
    public void MarkRefunded(string refundReason)
    {
        if (string.IsNullOrWhiteSpace(refundReason))
        {
            throw new ArgumentException("Refund reason cannot be empty.", nameof(refundReason));
        }

        if (Status != PaymentStatus.Succeeded)
        {
            throw new InvalidPaymentStateTransitionException(
                Status,
                PaymentStatus.Refunded,
                $"Cannot refund payment attempt in status {Status}. Only Succeeded payments can be refunded.");
        }

        Status = PaymentStatus.Refunded;
        FailureReason = refundReason.Trim();
        UpdatedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new PaymentAttemptRefundedDomainEvent(Id, OrderId, refundReason, Amount, Currency));
    }
}

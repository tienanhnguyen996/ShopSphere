using ShopSphere.BuildingBlocks.Common.Domain;

namespace ShopSphere.Services.Payment.Domain.Events;

public record PaymentAttemptCreatedDomainEvent(
    Guid PaymentAttemptId,
    Guid OrderId,
    decimal Amount,
    string Currency,
    string PaymentMethod) : IDomainEvent;

public record PaymentAttemptUnknownDomainEvent(
    Guid PaymentAttemptId,
    Guid OrderId,
    string Reason) : IDomainEvent;

public record PaymentAttemptSucceededDomainEvent(
    Guid PaymentAttemptId,
    Guid OrderId,
    string ProviderTransactionId,
    decimal Amount,
    string Currency) : IDomainEvent;

public record PaymentAttemptFailedDomainEvent(
    Guid PaymentAttemptId,
    Guid OrderId,
    string Reason,
    string? ProviderTransactionId) : IDomainEvent;

public record PaymentAttemptRefundedDomainEvent(
    Guid PaymentAttemptId,
    Guid OrderId,
    string Reason,
    decimal Amount,
    string Currency) : IDomainEvent;

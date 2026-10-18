using FluentAssertions;
using ShopSphere.Services.Payment.Domain;
using ShopSphere.Services.Payment.Domain.Events;
using ShopSphere.Services.Payment.Domain.Exceptions;
using Xunit;

namespace ShopSphere.Services.Payment.Tests.Unit;

public class PaymentAttemptTests
{
    [Fact]
    public void Create_WithValidParameters_InitializesCorrectly()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var amount = 99.50m;
        var currency = "USD";
        var method = "credit_card";

        // Act
        var attempt = PaymentAttempt.Create(orderId, amount, currency, method);

        // Assert
        attempt.Id.Should().NotBeEmpty();
        attempt.OrderId.Should().Be(orderId);
        attempt.Amount.Should().Be(amount);
        attempt.Currency.Should().Be("USD");
        attempt.PaymentMethod.Should().Be("credit_card");
        attempt.Status.Should().Be(PaymentStatus.Pending);
        attempt.ProviderTransactionId.Should().BeNull();
        attempt.FailureReason.Should().BeNull();

        attempt.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PaymentAttemptCreatedDomainEvent>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Create_WithInvalidAmount_ThrowsArgumentOutOfRangeException(decimal invalidAmount)
    {
        // Act
        var act = () => PaymentAttempt.Create(Guid.NewGuid(), invalidAmount, "USD", "credit_card");

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("amount");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("US")]
    [InlineData("USDT")]
    public void Create_WithInvalidCurrency_ThrowsArgumentException(string invalidCurrency)
    {
        // Act
        var act = () => PaymentAttempt.Create(Guid.NewGuid(), 50m, invalidCurrency, "credit_card");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("currency");
    }

    [Fact]
    public void Create_WithEmptyOrderId_ThrowsArgumentException()
    {
        // Act
        var act = () => PaymentAttempt.Create(Guid.Empty, 50m, "USD", "credit_card");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("orderId");
    }

    [Fact]
    public void MarkUnknown_FromPending_TransitionsToPaymentUnknown()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(Guid.NewGuid(), 100m, "USD", "card");
        attempt.ClearDomainEvents();

        // Act
        attempt.MarkUnknown("Provider connection timed out after 5000ms");

        // Assert
        attempt.Status.Should().Be(PaymentStatus.PaymentUnknown);
        attempt.FailureReason.Should().Be("Provider connection timed out after 5000ms");
        attempt.UpdatedAtUtc.Should().NotBeNull();
        attempt.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PaymentAttemptUnknownDomainEvent>();
    }

    [Fact]
    public void MarkSucceeded_FromPaymentUnknown_TransitionsToSucceeded()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(Guid.NewGuid(), 100m, "USD", "card");
        attempt.MarkUnknown("Timeout");
        attempt.ClearDomainEvents();

        // Act
        attempt.MarkSucceeded("tx_123456");

        // Assert
        attempt.Status.Should().Be(PaymentStatus.Succeeded);
        attempt.ProviderTransactionId.Should().Be("tx_123456");
        attempt.FailureReason.Should().BeNull();
        attempt.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PaymentAttemptSucceededDomainEvent>();
    }

    [Fact]
    public void MarkFailed_FromPaymentUnknown_TransitionsToFailed()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(Guid.NewGuid(), 100m, "USD", "card");
        attempt.MarkUnknown("Timeout");
        attempt.ClearDomainEvents();

        // Act
        attempt.MarkFailed("Insufficient funds on customer card", "tx_failed_999");

        // Assert
        attempt.Status.Should().Be(PaymentStatus.Failed);
        attempt.FailureReason.Should().Be("Insufficient funds on customer card");
        attempt.ProviderTransactionId.Should().Be("tx_failed_999");
        attempt.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PaymentAttemptFailedDomainEvent>();
    }

    [Fact]
    public void MarkSucceeded_WhenAlreadySucceeded_IsIdempotent()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(Guid.NewGuid(), 100m, "USD", "card");
        attempt.MarkSucceeded("tx_123");
        var eventCount = attempt.DomainEvents.Count;

        // Act
        attempt.MarkSucceeded("tx_123");

        // Assert
        attempt.Status.Should().Be(PaymentStatus.Succeeded);
        attempt.DomainEvents.Count.Should().Be(eventCount);
    }

    [Fact]
    public void MarkFailed_WhenAlreadyFailed_IsIdempotent()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(Guid.NewGuid(), 100m, "USD", "card");
        attempt.MarkFailed("Card expired");
        var eventCount = attempt.DomainEvents.Count;

        // Act
        attempt.MarkFailed("Card expired");

        // Assert
        attempt.Status.Should().Be(PaymentStatus.Failed);
        attempt.DomainEvents.Count.Should().Be(eventCount);
    }

    [Fact]
    public void MarkRefunded_FromSucceeded_TransitionsToRefunded()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(Guid.NewGuid(), 100m, "USD", "card");
        attempt.MarkSucceeded("tx_123");
        attempt.ClearDomainEvents();

        // Act
        attempt.MarkRefunded("Customer requested cancellation");

        // Assert
        attempt.Status.Should().Be(PaymentStatus.Refunded);
        attempt.FailureReason.Should().Be("Customer requested cancellation");
        attempt.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PaymentAttemptRefundedDomainEvent>();
    }

    [Fact]
    public void MarkRefunded_WhenNotSucceeded_ThrowsInvalidPaymentStateTransitionException()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(Guid.NewGuid(), 100m, "USD", "card");

        // Act
        var act = () => attempt.MarkRefunded("Customer refund");

        // Assert
        act.Should().Throw<InvalidPaymentStateTransitionException>()
            .Where(e => e.CurrentStatus == PaymentStatus.Pending && e.TargetStatus == PaymentStatus.Refunded);
    }

    [Fact]
    public void MarkUnknown_FromSucceeded_ThrowsInvalidPaymentStateTransitionException()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(Guid.NewGuid(), 100m, "USD", "card");
        attempt.MarkSucceeded("tx_123");

        // Act
        var act = () => attempt.MarkUnknown("Late timeout");

        // Assert
        act.Should().Throw<InvalidPaymentStateTransitionException>();
    }

    [Fact]
    public void MarkSucceeded_FromFailed_ThrowsInvalidPaymentStateTransitionException()
    {
        // Arrange
        var attempt = PaymentAttempt.Create(Guid.NewGuid(), 100m, "USD", "card");
        attempt.MarkFailed("Card blocked");

        // Act
        var act = () => attempt.MarkSucceeded("tx_late_success");

        // Assert
        act.Should().Throw<InvalidPaymentStateTransitionException>();
    }
}

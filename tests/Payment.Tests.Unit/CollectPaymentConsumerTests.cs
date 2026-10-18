using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Payment.Application.Common.Interfaces;
using ShopSphere.Services.Payment.Application.Consumers;
using ShopSphere.Services.Payment.Application.Contracts;
using ShopSphere.Services.Payment.Domain;
using ShopSphere.Services.Payment.Tests.Unit.Helpers;
using Xunit;

namespace ShopSphere.Services.Payment.Tests.Unit;

public class CollectPaymentConsumerTests
{
    private readonly Mock<IPaymentDbContext> _mockDbContext;
    private readonly Mock<IPaymentProviderClient> _mockProviderClient;
    private readonly Mock<ILogger<CollectPaymentConsumer>> _mockLogger;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;

    public CollectPaymentConsumerTests()
    {
        _mockDbContext = new Mock<IPaymentDbContext>();
        _mockProviderClient = new Mock<IPaymentProviderClient>();
        _mockLogger = new Mock<ILogger<CollectPaymentConsumer>>();
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
    }

    [Fact]
    public async Task HandlePaymentAsync_WhenProviderApproves_SetsStatusSucceededAndPublishesPaymentSucceeded()
    {
        // Arrange
        var attempts = new List<PaymentAttempt>();
        var mockSet = MockDbSetExtensions.CreateMockDbSet(attempts);
        _mockDbContext.Setup(c => c.PaymentAttempts).Returns(mockSet.Object);

        _mockProviderClient
            .Setup(c => c.ChargeAsync(It.IsAny<PaymentChargeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentChargeResult(PaymentProviderStatus.Success, "tx_prov_success_123", null));

        var consumer = new CollectPaymentConsumer(_mockDbContext.Object, _mockProviderClient.Object, _mockLogger.Object);
        var orderId = Guid.NewGuid();
        var command = new CollectPayment(orderId, 199.99m, "USD", "credit_card");

        // Act
        await consumer.HandlePaymentAsync(command, _mockPublishEndpoint.Object);

        // Assert
        attempts.Should().ContainSingle();
        var created = attempts.First();
        created.Status.Should().Be(PaymentStatus.Succeeded);
        created.ProviderTransactionId.Should().Be("tx_prov_success_123");

        _mockPublishEndpoint.Verify(
            p => p.Publish(It.Is<PaymentSucceeded>(e => e.OrderId == orderId && e.ProviderTransactionId == "tx_prov_success_123"), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockPublishEndpoint.Verify(
            p => p.Publish(It.Is<MessageEnvelope<PaymentSucceeded>>(e => e.Payload.OrderId == orderId), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockDbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task HandlePaymentAsync_WhenProviderDeclines_SetsStatusFailedAndPublishesPaymentRejected()
    {
        // Arrange
        var attempts = new List<PaymentAttempt>();
        var mockSet = MockDbSetExtensions.CreateMockDbSet(attempts);
        _mockDbContext.Setup(c => c.PaymentAttempts).Returns(mockSet.Object);

        _mockProviderClient
            .Setup(c => c.ChargeAsync(It.IsAny<PaymentChargeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentChargeResult(PaymentProviderStatus.Failed, null, "Card declined: insufficient funds"));

        var consumer = new CollectPaymentConsumer(_mockDbContext.Object, _mockProviderClient.Object, _mockLogger.Object);
        var orderId = Guid.NewGuid();
        var command = new CollectPayment(orderId, 50m, "USD", "mock_declined");

        // Act
        await consumer.HandlePaymentAsync(command, _mockPublishEndpoint.Object);

        // Assert
        attempts.Should().ContainSingle();
        var created = attempts.First();
        created.Status.Should().Be(PaymentStatus.Failed);
        created.FailureReason.Should().Contain("insufficient funds");

        _mockPublishEndpoint.Verify(
            p => p.Publish(It.Is<PaymentRejected>(e => e.OrderId == orderId && e.Reason.Contains("insufficient funds")), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockPublishEndpoint.Verify(
            p => p.Publish(It.IsAny<PaymentSucceeded>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandlePaymentAsync_WhenProviderTimesOut_SetsStatusPaymentUnknownAndNeverPublishesPaymentRejected()
    {
        // Arrange
        var attempts = new List<PaymentAttempt>();
        var mockSet = MockDbSetExtensions.CreateMockDbSet(attempts);
        _mockDbContext.Setup(c => c.PaymentAttempts).Returns(mockSet.Object);

        _mockProviderClient
            .Setup(c => c.ChargeAsync(It.IsAny<PaymentChargeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentChargeResult(PaymentProviderStatus.Unknown, null, "Provider HTTP call timed out after 5000ms. Status is unknown."));

        var consumer = new CollectPaymentConsumer(_mockDbContext.Object, _mockProviderClient.Object, _mockLogger.Object);
        var orderId = Guid.NewGuid();
        var command = new CollectPayment(orderId, 100m, "USD", "mock_timeout");

        // Act
        await consumer.HandlePaymentAsync(command, _mockPublishEndpoint.Object);

        // Assert: Timeout = Unknown, NOT Failure!
        attempts.Should().ContainSingle();
        var created = attempts.First();
        created.Status.Should().Be(PaymentStatus.PaymentUnknown);
        created.FailureReason.Should().Contain("timed out");

        _mockPublishEndpoint.Verify(
            p => p.Publish(It.Is<PaymentPendingResolution>(e => e.OrderId == orderId), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockPublishEndpoint.Verify(
            p => p.Publish(It.IsAny<PaymentRejected>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _mockPublishEndpoint.Verify(
            p => p.Publish(It.IsAny<PaymentSucceeded>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandlePaymentAsync_WhenAttemptAlreadySucceeded_PublishesPaymentSucceededWithoutChargingAgain()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var existing = PaymentAttempt.Create(orderId, 120m, "USD", "credit_card");
        existing.MarkSucceeded("tx_existing_888");

        var attempts = new List<PaymentAttempt> { existing };
        var mockSet = MockDbSetExtensions.CreateMockDbSet(attempts);
        _mockDbContext.Setup(c => c.PaymentAttempts).Returns(mockSet.Object);

        var consumer = new CollectPaymentConsumer(_mockDbContext.Object, _mockProviderClient.Object, _mockLogger.Object);
        var command = new CollectPayment(orderId, 120m, "USD", "credit_card");

        // Act
        await consumer.HandlePaymentAsync(command, _mockPublishEndpoint.Object);

        // Assert
        _mockProviderClient.Verify(c => c.ChargeAsync(It.IsAny<PaymentChargeRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockPublishEndpoint.Verify(
            p => p.Publish(It.Is<PaymentSucceeded>(e => e.OrderId == orderId && e.ProviderTransactionId == "tx_existing_888"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandlePaymentAsync_WhenAttemptAlreadyFailed_PublishesPaymentRejectedWithoutChargingAgain()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var existing = PaymentAttempt.Create(orderId, 120m, "USD", "credit_card");
        existing.MarkFailed("Previously declined");

        var attempts = new List<PaymentAttempt> { existing };
        var mockSet = MockDbSetExtensions.CreateMockDbSet(attempts);
        _mockDbContext.Setup(c => c.PaymentAttempts).Returns(mockSet.Object);

        var consumer = new CollectPaymentConsumer(_mockDbContext.Object, _mockProviderClient.Object, _mockLogger.Object);
        var command = new CollectPayment(orderId, 120m, "USD", "credit_card");

        // Act
        await consumer.HandlePaymentAsync(command, _mockPublishEndpoint.Object);

        // Assert
        _mockProviderClient.Verify(c => c.ChargeAsync(It.IsAny<PaymentChargeRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockPublishEndpoint.Verify(
            p => p.Publish(It.Is<PaymentRejected>(e => e.OrderId == orderId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandlePaymentAsync_WhenAttemptAlreadyPaymentUnknown_DoesNotChargeAgain()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var existing = PaymentAttempt.Create(orderId, 120m, "USD", "credit_card");
        existing.MarkUnknown("Awaiting reconciliation from prior attempt");

        var attempts = new List<PaymentAttempt> { existing };
        var mockSet = MockDbSetExtensions.CreateMockDbSet(attempts);
        _mockDbContext.Setup(c => c.PaymentAttempts).Returns(mockSet.Object);

        var consumer = new CollectPaymentConsumer(_mockDbContext.Object, _mockProviderClient.Object, _mockLogger.Object);
        var command = new CollectPayment(orderId, 120m, "USD", "credit_card");

        // Act
        await consumer.HandlePaymentAsync(command, _mockPublishEndpoint.Object);

        // Assert
        _mockProviderClient.Verify(c => c.ChargeAsync(It.IsAny<PaymentChargeRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockPublishEndpoint.Verify(p => p.Publish(It.IsAny<PaymentSucceeded>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockPublishEndpoint.Verify(p => p.Publish(It.IsAny<PaymentRejected>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

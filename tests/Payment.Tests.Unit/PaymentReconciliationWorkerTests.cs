using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Payment.Application.Common.Interfaces;
using ShopSphere.Services.Payment.Application.Contracts;
using ShopSphere.Services.Payment.Application.Workers;
using ShopSphere.Services.Payment.Domain;
using ShopSphere.Services.Payment.Tests.Unit.Helpers;
using Xunit;

namespace ShopSphere.Services.Payment.Tests.Unit;

public class PaymentReconciliationWorkerTests
{
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IPaymentDbContext> _mockDbContext;
    private readonly Mock<IPaymentProviderClient> _mockProviderClient;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly Mock<ILogger<PaymentReconciliationWorker>> _mockLogger;

    public PaymentReconciliationWorkerTests()
    {
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockDbContext = new Mock<IPaymentDbContext>();
        _mockProviderClient = new Mock<IPaymentProviderClient>();
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _mockLogger = new Mock<ILogger<PaymentReconciliationWorker>>();

        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(_mockScope.Object);
        _mockScope.Setup(s => s.ServiceProvider).Returns(_mockServiceProvider.Object);

        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IPaymentDbContext))).Returns(_mockDbContext.Object);
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IPaymentProviderClient))).Returns(_mockProviderClient.Object);
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(IPublishEndpoint))).Returns(_mockPublishEndpoint.Object);
    }

    [Fact]
    public async Task ReconcileUnknownPaymentsAsync_WhenNoUnknownAttempts_ReturnsZero()
    {
        // Arrange
        var attempts = new List<PaymentAttempt>();
        var mockSet = MockDbSetExtensions.CreateMockDbSet(attempts);
        _mockDbContext.Setup(c => c.PaymentAttempts).Returns(mockSet.Object);

        var worker = new PaymentReconciliationWorker(_mockScopeFactory.Object, _mockLogger.Object);

        // Act
        var count = await worker.ReconcileUnknownPaymentsAsync();

        // Assert
        count.Should().Be(0);
        _mockProviderClient.Verify(c => c.GetStatusAsync(It.IsAny<PaymentStatusQueryRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileUnknownPaymentsAsync_WhenProviderResolvesSuccess_TransitionsToSucceededAndPublishesEvent()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var attempt = PaymentAttempt.Create(orderId, 150m, "USD", "credit_card");
        attempt.MarkUnknown("Simulated initial timeout");

        var attempts = new List<PaymentAttempt> { attempt };
        var mockSet = MockDbSetExtensions.CreateMockDbSet(attempts);
        _mockDbContext.Setup(c => c.PaymentAttempts).Returns(mockSet.Object);

        _mockProviderClient
            .Setup(c => c.GetStatusAsync(It.Is<PaymentStatusQueryRequest>(q => q.PaymentAttemptId == attempt.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentStatusQueryResult(PaymentProviderStatus.Success, "tx_reconciled_999", null));

        var worker = new PaymentReconciliationWorker(_mockScopeFactory.Object, _mockLogger.Object);

        // Act
        var count = await worker.ReconcileUnknownPaymentsAsync();

        // Assert
        count.Should().Be(1);
        attempt.Status.Should().Be(PaymentStatus.Succeeded);
        attempt.ProviderTransactionId.Should().Be("tx_reconciled_999");

        _mockPublishEndpoint.Verify(
            p => p.Publish(It.Is<PaymentSucceeded>(e => e.OrderId == orderId && e.ProviderTransactionId == "tx_reconciled_999"), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockPublishEndpoint.Verify(
            p => p.Publish(It.Is<MessageEnvelope<PaymentSucceeded>>(e => e.Payload.OrderId == orderId), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockDbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReconcileUnknownPaymentsAsync_WhenProviderResolvesFailed_TransitionsToFailedAndPublishesRejectedEvent()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var attempt = PaymentAttempt.Create(orderId, 250m, "USD", "credit_card");
        attempt.MarkUnknown("Initial timeout");

        var attempts = new List<PaymentAttempt> { attempt };
        var mockSet = MockDbSetExtensions.CreateMockDbSet(attempts);
        _mockDbContext.Setup(c => c.PaymentAttempts).Returns(mockSet.Object);

        _mockProviderClient
            .Setup(c => c.GetStatusAsync(It.Is<PaymentStatusQueryRequest>(q => q.PaymentAttemptId == attempt.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentStatusQueryResult(PaymentProviderStatus.Failed, "tx_declined_111", "Card reported stolen"));

        var worker = new PaymentReconciliationWorker(_mockScopeFactory.Object, _mockLogger.Object);

        // Act
        var count = await worker.ReconcileUnknownPaymentsAsync();

        // Assert
        count.Should().Be(1);
        attempt.Status.Should().Be(PaymentStatus.Failed);
        attempt.FailureReason.Should().Be("Card reported stolen");

        _mockPublishEndpoint.Verify(
            p => p.Publish(It.Is<PaymentRejected>(e => e.OrderId == orderId && e.Reason == "Card reported stolen"), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockPublishEndpoint.Verify(
            p => p.Publish(It.Is<MessageEnvelope<PaymentRejected>>(e => e.Payload.OrderId == orderId), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockDbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReconcileUnknownPaymentsAsync_WhenProviderStillUnknown_KeepsPaymentUnknown()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var attempt = PaymentAttempt.Create(orderId, 100m, "USD", "credit_card");
        attempt.MarkUnknown("Initial timeout");

        var attempts = new List<PaymentAttempt> { attempt };
        var mockSet = MockDbSetExtensions.CreateMockDbSet(attempts);
        _mockDbContext.Setup(c => c.PaymentAttempts).Returns(mockSet.Object);

        _mockProviderClient
            .Setup(c => c.GetStatusAsync(It.IsAny<PaymentStatusQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentStatusQueryResult(PaymentProviderStatus.Unknown, null, "Still pending at bank"));

        var worker = new PaymentReconciliationWorker(_mockScopeFactory.Object, _mockLogger.Object);

        // Act
        var count = await worker.ReconcileUnknownPaymentsAsync();

        // Assert
        count.Should().Be(0);
        attempt.Status.Should().Be(PaymentStatus.PaymentUnknown);

        _mockPublishEndpoint.Verify(p => p.Publish(It.IsAny<PaymentSucceeded>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockPublishEndpoint.Verify(p => p.Publish(It.IsAny<PaymentRejected>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockDbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReconcileUnknownPaymentsAsync_WhenConcurrencyConflictOnFirstAttempt_RetriesAndSucceeds()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var attempt = PaymentAttempt.Create(orderId, 150m, "USD", "credit_card");
        attempt.MarkUnknown("Initial timeout");

        var attempts = new List<PaymentAttempt> { attempt };
        var mockSet = MockDbSetExtensions.CreateMockDbSet(attempts);
        _mockDbContext.Setup(c => c.PaymentAttempts).Returns(mockSet.Object);

        _mockProviderClient
            .Setup(c => c.GetStatusAsync(It.Is<PaymentStatusQueryRequest>(q => q.PaymentAttemptId == attempt.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentStatusQueryResult(PaymentProviderStatus.Success, "tx_reconciled_retry", null));

        // First SaveChangesAsync throws concurrency exception, second succeeds
        var saveCallCount = 0;
        _mockDbContext
            .Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                saveCallCount++;
                if (saveCallCount == 1)
                {
                    throw new DbUpdateConcurrencyException("Simulated conflict on first save");
                }
                return Task.FromResult(1);
            });

        var worker = new PaymentReconciliationWorker(_mockScopeFactory.Object, _mockLogger.Object);

        // Act
        var count = await worker.ReconcileUnknownPaymentsAsync();

        // Assert
        count.Should().Be(1);
        attempt.Status.Should().Be(PaymentStatus.Succeeded);
        attempt.ProviderTransactionId.Should().Be("tx_reconciled_retry");
        saveCallCount.Should().Be(2);

        _mockPublishEndpoint.Verify(
            p => p.Publish(It.Is<PaymentSucceeded>(e => e.OrderId == orderId && e.ProviderTransactionId == "tx_reconciled_retry"), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

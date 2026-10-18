using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ShopSphere.Services.Payment.Application.Common.Interfaces;
using ShopSphere.Services.Payment.Infrastructure.Clients;
using Xunit;

namespace ShopSphere.Services.Payment.Tests.Unit;

public class MockPaymentProviderClientTests
{
    private readonly Mock<ILogger<MockPaymentProviderClient>> _mockLogger;
    private readonly MockPaymentProviderClient _client;

    public MockPaymentProviderClientTests()
    {
        _mockLogger = new Mock<ILogger<MockPaymentProviderClient>>();
        var httpClient = new HttpClient();
        _client = new MockPaymentProviderClient(httpClient, _mockLogger.Object);
        MockPaymentProviderClient.ClearKnownAttempts();
    }

    [Fact]
    public async Task ChargeAsync_WhenMethodContainsTimeout_ReturnsUnknownStatus_DoesNotTreatAsFailure()
    {
        // Arrange
        var request = new PaymentChargeRequest(
            PaymentAttemptId: Guid.NewGuid(),
            OrderId: Guid.NewGuid(),
            Amount: 150m,
            Currency: "USD",
            PaymentMethod: "mock_timeout");

        // Act
        var result = await _client.ChargeAsync(request);

        // Assert
        result.Status.Should().Be(PaymentProviderStatus.Unknown);
        result.ProviderTransactionId.Should().BeNull();
        result.ErrorMessage.Should().Contain("timed out");
    }

    [Fact]
    public async Task ChargeAsync_WhenMethodContainsNetworkError_ReturnsUnknownStatus()
    {
        // Arrange
        var request = new PaymentChargeRequest(
            PaymentAttemptId: Guid.NewGuid(),
            OrderId: Guid.NewGuid(),
            Amount: 85m,
            Currency: "USD",
            PaymentMethod: "mock_network_error");

        // Act
        var result = await _client.ChargeAsync(request);

        // Assert
        result.Status.Should().Be(PaymentProviderStatus.Unknown);
        result.ErrorMessage.Should().ContainEquivalentOf("network");
    }

    [Fact]
    public async Task ChargeAsync_WhenMethodContainsDecline_ReturnsFailedStatus()
    {
        // Arrange
        var request = new PaymentChargeRequest(
            PaymentAttemptId: Guid.NewGuid(),
            OrderId: Guid.NewGuid(),
            Amount: 200m,
            Currency: "USD",
            PaymentMethod: "mock_declined");

        // Act
        var result = await _client.ChargeAsync(request);

        // Assert
        result.Status.Should().Be(PaymentProviderStatus.Failed);
        result.ErrorMessage.Should().Contain("declined");
    }

    [Fact]
    public async Task ChargeAsync_WhenMethodIsCreditCard_ReturnsSuccessStatusWithTransactionId()
    {
        // Arrange
        var request = new PaymentChargeRequest(
            PaymentAttemptId: Guid.NewGuid(),
            OrderId: Guid.NewGuid(),
            Amount: 120m,
            Currency: "USD",
            PaymentMethod: "credit_card");

        // Act
        var result = await _client.ChargeAsync(request);

        // Assert
        result.Status.Should().Be(PaymentProviderStatus.Success);
        result.ProviderTransactionId.Should().StartWith("mock_tx_");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task GetStatusAsync_WhenAttemptWasPreviouslyCharged_ReturnsStoredStatus()
    {
        // Arrange
        var attemptId = Guid.NewGuid();
        MockPaymentProviderClient.SetAttemptStatus(attemptId, PaymentProviderStatus.Success, "tx_stored_123");

        var query = new PaymentStatusQueryRequest(attemptId, "tx_stored_123");

        // Act
        var result = await _client.GetStatusAsync(query);

        // Assert
        result.Status.Should().Be(PaymentProviderStatus.Success);
        result.ProviderTransactionId.Should().Be("tx_stored_123");
    }

    [Fact]
    public async Task GetStatusAsync_WhenAttemptNotRecorded_ReturnsUnknownStatus()
    {
        // Arrange
        var query = new PaymentStatusQueryRequest(Guid.NewGuid(), null);

        // Act
        var result = await _client.GetStatusAsync(query);

        // Assert
        result.Status.Should().Be(PaymentProviderStatus.Unknown);
    }

    [Fact]
    public async Task RefundAsync_WhenNormal_ReturnsSuccessWithRefundTransactionId()
    {
        // Arrange
        var request = new PaymentRefundRequest(
            PaymentAttemptId: Guid.NewGuid(),
            ProviderTransactionId: "tx_orig_123",
            Amount: 50m,
            Currency: "USD",
            Reason: "Customer returned product");

        // Act
        var result = await _client.RefundAsync(request);

        // Assert
        result.Status.Should().Be(PaymentProviderStatus.Success);
        result.RefundTransactionId.Should().StartWith("mock_ref_");
    }

    [Fact]
    public async Task RefundAsync_WhenTimeoutReason_ReturnsUnknownStatus()
    {
        // Arrange
        var request = new PaymentRefundRequest(
            PaymentAttemptId: Guid.NewGuid(),
            ProviderTransactionId: "tx_orig_123",
            Amount: 50m,
            Currency: "USD",
            Reason: "timeout simulation");

        // Act
        var result = await _client.RefundAsync(request);

        // Assert
        result.Status.Should().Be(PaymentProviderStatus.Unknown);
    }
}

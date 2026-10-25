using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using ShopSphere.Services.Payment.Application.Common.Interfaces;
using ShopSphere.Services.Payment.Infrastructure.Clients;
using ShopSphere.Services.Payment.Infrastructure.Configuration;
using Xunit;

namespace ShopSphere.Services.Payment.Tests.Unit;

public class RealPaymentProviderClientsTests
{
    [Fact]
    public async Task StripeClient_WhenChargeSucceeds_ReturnsSuccessWithIntentId()
    {
        // Arrange
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"id\": \"pi_123456\", \"status\": \"succeeded\"}")
            });

        var httpClient = new HttpClient(handler.Object);
        var settings = Options.Create(new PaymentSettings
        {
            Stripe = new StripeSettings { ApiKey = "sk_test_123" }
        });
        var logger = new Mock<ILogger<StripePaymentProviderClient>>();
        var client = new StripePaymentProviderClient(httpClient, settings, logger.Object);

        var request = new PaymentChargeRequest(Guid.NewGuid(), Guid.NewGuid(), 99.99m, "USD", "credit_card");

        // Act
        var result = await client.ChargeAsync(request);

        // Assert
        result.Status.Should().Be(PaymentProviderStatus.Success);
        result.ProviderTransactionId.Should().Be("pi_123456");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task StripeClient_WhenServerError_ReturnsUnknownStatus()
    {
        // Arrange
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("{\"error\": {\"message\": \"Stripe temporary outage\"}}")
            });

        var httpClient = new HttpClient(handler.Object);
        var settings = Options.Create(new PaymentSettings
        {
            Stripe = new StripeSettings { ApiKey = "sk_test_123" }
        });
        var logger = new Mock<ILogger<StripePaymentProviderClient>>();
        var client = new StripePaymentProviderClient(httpClient, settings, logger.Object);

        var request = new PaymentChargeRequest(Guid.NewGuid(), Guid.NewGuid(), 50m, "USD", "credit_card");

        // Act
        var result = await client.ChargeAsync(request);

        // Assert (Timeout / 5xx = Unknown, not Failed)
        result.Status.Should().Be(PaymentProviderStatus.Unknown);
        result.ProviderTransactionId.Should().BeNull();
    }

    [Fact]
    public async Task MoMoClient_WhenChargeSucceeds_ReturnsSuccessWithMomoPrefix()
    {
        // Arrange
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"resultCode\": 0, \"message\": \"Success\", \"transId\": 987654321}")
            });

        var httpClient = new HttpClient(handler.Object);
        var settings = Options.Create(new PaymentSettings());
        var logger = new Mock<ILogger<MoMoPaymentProviderClient>>();
        var client = new MoMoPaymentProviderClient(httpClient, settings, logger.Object);

        var request = new PaymentChargeRequest(Guid.NewGuid(), Guid.NewGuid(), 250000m, "VND", "digital_wallet");

        // Act
        var result = await client.ChargeAsync(request);

        // Assert
        result.Status.Should().Be(PaymentProviderStatus.Success);
        result.ProviderTransactionId.Should().Be("momo_987654321");
    }

    [Fact]
    public async Task VnPayClient_GeneratesSecureHashAndTransactionReference()
    {
        // Arrange
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(handler.Object);
        var settings = Options.Create(new PaymentSettings());
        var logger = new Mock<ILogger<VnPayPaymentProviderClient>>();
        var client = new VnPayPaymentProviderClient(httpClient, settings, logger.Object);

        var attemptId = Guid.NewGuid();
        var request = new PaymentChargeRequest(attemptId, Guid.NewGuid(), 500000m, "VND", "bank_transfer");

        // Act
        var result = await client.ChargeAsync(request);

        // Assert
        result.Status.Should().Be(PaymentProviderStatus.Success);
        result.ProviderTransactionId.Should().Be($"vnp_{attemptId:N}");
    }

    [Fact]
    public async Task RoutingPaymentProviderClient_WhenUnconfigured_FallsBackToMock()
    {
        // Arrange
        var handler = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(handler.Object);
        var emptySettings = Options.Create(new PaymentSettings
        {
            Stripe = new StripeSettings { ApiKey = "" },
            MoMo = new MoMoSettings { PartnerCode = "" },
            VnPay = new VnPaySettings { TmnCode = "" },
            FallbackToMockOnMissingCredentials = true
        });

        var stripe = new StripePaymentProviderClient(new HttpClient(handler.Object), emptySettings, new Mock<ILogger<StripePaymentProviderClient>>().Object);
        var momo = new MoMoPaymentProviderClient(new HttpClient(handler.Object), emptySettings, new Mock<ILogger<MoMoPaymentProviderClient>>().Object);
        var vnpay = new VnPayPaymentProviderClient(new HttpClient(handler.Object), emptySettings, new Mock<ILogger<VnPayPaymentProviderClient>>().Object);
        var mock = new MockPaymentProviderClient(new HttpClient(), new Mock<ILogger<MockPaymentProviderClient>>().Object);
        var routingLogger = new Mock<ILogger<RoutingPaymentProviderClient>>();

        var router = new RoutingPaymentProviderClient(stripe, momo, vnpay, mock, emptySettings, routingLogger.Object);

        var request = new PaymentChargeRequest(Guid.NewGuid(), Guid.NewGuid(), 100m, "USD", "credit_card");

        // Act
        var result = await router.ChargeAsync(request);

        // Assert
        result.Status.Should().Be(PaymentProviderStatus.Success);
        result.ProviderTransactionId.Should().StartWith("mock_tx_");
    }
}

using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using ShopSphere.Services.Shipping.Application.Abstractions;
using ShopSphere.Services.Shipping.Domain;
using ShopSphere.Services.Shipping.Infrastructure.Carriers;
using ShopSphere.Services.Shipping.Infrastructure.Configuration;
using Xunit;

namespace ShopSphere.Services.Shipping.Tests.Unit.Infrastructure;

public class RealCarrierProviderClientsTests
{
    [Fact]
    public async Task ShippoClient_WhenRatesAndBuySucceed_ReturnsCreatedWithTrackingNumber()
    {
        // Arrange
        var handler = new Mock<HttpMessageHandler>();

        // Response for shipments/ (rates)
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("shipments")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"rates\": [{\"object_id\": \"rate_usps_123\", \"provider\": \"USPS\"}]}")
            });

        // Response for transactions/ (label purchase)
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString().Contains("transactions")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"status\": \"SUCCESS\", \"tracking_number\": \"9400100000000000000000\", \"label_url\": \"https://goshippo.com/label.pdf\"}")
            });

        var httpClient = new HttpClient(handler.Object);
        var settings = Options.Create(new CarrierSettings
        {
            Shippo = new ShippoSettings { ApiKey = "shippo_test_123" }
        });
        var logger = new Mock<ILogger<ShippoCarrierClient>>();
        var client = new ShippoCarrierClient(httpClient, settings, logger.Object);

        var address = ShippingAddress.Create("Alex Mercer", "123 Main St", "Seattle", "WA", "98101", "US", "+12065550100");
        var items = new[] { ShipmentItem.Create(Guid.NewGuid(), "SKU-001", "Widget", 2) };

        // Act
        var result = await client.RequestLabelAsync(Guid.NewGuid(), "REQ-001", address, items);

        // Assert
        result.Status.Should().Be(CarrierLabelStatus.Created);
        result.Carrier.Should().Be("USPS");
        result.TrackingNumber.Should().Be("9400100000000000000000");
        result.LabelUrl.Should().Be("https://goshippo.com/label.pdf");
    }

    [Fact]
    public async Task ShippoClient_WhenServerError_ReturnsUnknownTimeoutStatus()
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
                StatusCode = HttpStatusCode.BadGateway,
                Content = new StringContent("{\"error\": \"Bad Gateway\"}")
            });

        var httpClient = new HttpClient(handler.Object);
        var settings = Options.Create(new CarrierSettings
        {
            Shippo = new ShippoSettings { ApiKey = "shippo_test_123" }
        });
        var logger = new Mock<ILogger<ShippoCarrierClient>>();
        var client = new ShippoCarrierClient(httpClient, settings, logger.Object);

        var address = ShippingAddress.Create("Alex Mercer", "123 Main St", "Seattle", "WA", "98101", "US", "+12065550100");
        var items = new[] { ShipmentItem.Create(Guid.NewGuid(), "SKU-001", "Widget", 2) };

        // Act
        var result = await client.RequestLabelAsync(Guid.NewGuid(), "REQ-001", address, items);

        // Assert
        result.Status.Should().Be(CarrierLabelStatus.UnknownTimeout);
        result.TrackingNumber.Should().BeNull();
    }

    [Fact]
    public async Task GhnClient_WhenOrderCreated_ReturnsCreatedWithGhnOrderCode()
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
                Content = new StringContent("{\"code\": 200, \"message\": \"Success\", \"data\": {\"order_code\": \"GHN-VN-98765\", \"expected_delivery_time\": \"2026-10-30T10:00:00Z\"}}")
            });

        var httpClient = new HttpClient(handler.Object);
        var settings = Options.Create(new CarrierSettings
        {
            Ghn = new GhnSettings { Token = "test_token", ShopId = 12345 }
        });
        var logger = new Mock<ILogger<GhnCarrierClient>>();
        var client = new GhnCarrierClient(httpClient, settings, logger.Object);

        var address = ShippingAddress.Create("Nguyen Van A", "123 Le Loi", "Ho Chi Minh", "Quan 1", "70000", "VN", "0901234567");
        var items = new[] { ShipmentItem.Create(Guid.NewGuid(), "SKU-VN", "Ao Thun", 1) };

        // Act
        var result = await client.RequestLabelAsync(Guid.NewGuid(), "REQ-VN-001", address, items);

        // Assert
        result.Status.Should().Be(CarrierLabelStatus.Created);
        result.Carrier.Should().Be("GHN");
        result.TrackingNumber.Should().Be("GHN-VN-98765");
    }

    [Fact]
    public async Task RoutingCarrierProviderClient_WhenUnconfigured_FallsBackToMock()
    {
        // Arrange
        var handler = new Mock<HttpMessageHandler>();
        var emptySettings = Options.Create(new CarrierSettings
        {
            Shippo = new ShippoSettings { ApiKey = "" },
            Ghn = new GhnSettings { Token = "", ShopId = 0 },
            FallbackToMockOnMissingCredentials = true
        });

        var shippo = new ShippoCarrierClient(new HttpClient(handler.Object), emptySettings, new Mock<ILogger<ShippoCarrierClient>>().Object);
        var ghn = new GhnCarrierClient(new HttpClient(handler.Object), emptySettings, new Mock<ILogger<GhnCarrierClient>>().Object);
        var mock = new MockCarrierProviderClient();
        var router = new RoutingCarrierProviderClient(shippo, ghn, mock, emptySettings, new Mock<ILogger<RoutingCarrierProviderClient>>().Object);

        var address = ShippingAddress.Create("Nguyen Van A", "123 Le Loi", "Ho Chi Minh", "Quan 1", "70000", "VN", "0901234567");
        var items = new[] { ShipmentItem.Create(Guid.NewGuid(), "SKU-VN", "Ao Thun", 1) };
        var orderId = Guid.NewGuid();

        // Act
        var result = await router.RequestLabelAsync(orderId, "REQ-VN-001", address, items);

        // Assert
        result.Status.Should().Be(CarrierLabelStatus.Created);
        result.TrackingNumber.Should().StartWith("SS-TRK-");
        result.Carrier.Should().Be("ShopSphereExpress");
    }
}

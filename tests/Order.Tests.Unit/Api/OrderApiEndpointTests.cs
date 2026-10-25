using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.BuildingBlocks.Idempotency;
using ShopSphere.Services.Order.Api;
using ShopSphere.Services.Order.Application;
using ShopSphere.Services.Order.Application.Common.Interfaces;
using ShopSphere.Services.Order.Application.Contracts;
using ShopSphere.Services.Order.Application.Orders.Dtos;
using ShopSphere.Services.Order.Infrastructure.Idempotency;
using ShopSphere.Services.Order.Tests.Unit.Helpers;
using Xunit;

namespace ShopSphere.Services.Order.Tests.Unit.Api;

public class OrderApiEndpointTests
{
    private static ShippingAddressRequest CreateAddressRequest() =>
        new("Jane Doe", "123 Market St", "San Francisco", "CA", "94105", "US", "555-0199");

    [Fact]
    public async Task Checkout_WithoutIdempotencyKeyHeader_ShouldReturn400BadRequest()
    {
        await using var app = await CreateTestAppAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Customer-Id", "cust-123");

        var response = await client.PostAsJsonAsync(
            "/api/v1/orders/checkout",
            new CheckoutRequest(CreateAddressRequest(), "credit_card"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Checkout_WithValidCartQuoteAndIdempotencyKey_ShouldCreateOrderReplayIdempotentlyAndRejectPayloadMismatch()
    {
        var orders = new List<ShopSphere.Services.Order.Domain.Order>();
        var idempotencyRecords = new List<IdempotencyRecord>();
        var publishMock = new Mock<IPublishEndpoint>();
        var cartClientMock = new Mock<ICartClient>();

        cartClientMock
            .Setup(c => c.GetCheckoutQuoteAsync("cust-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CartCheckoutQuoteDto(
                "cust-123",
                [new CartQuoteItemDto("SKU-LAPTOP-1", "Pro Laptop", 1, 1499.00m, 1499.00m, "USD", 4, true)],
                1499.00m,
                "USD",
                new Dictionary<string, int> { ["SKU-LAPTOP-1"] = 4 },
                true,
                [])));

        cartClientMock
            .Setup(c => c.ClearCartAsync("cust-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        await using var app = await CreateTestAppAsync(orders, idempotencyRecords, cartClientMock.Object, publishMock.Object);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Customer-Id", "cust-123");

        var checkoutPayload = new CheckoutRequest(CreateAddressRequest(), "credit_card");

        // 1. First request -> 201 Created
        using var firstReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders/checkout")
        {
            Content = JsonContent.Create(checkoutPayload)
        };
        firstReq.Headers.Add("Idempotency-Key", "idem-checkout-001");

        var firstResponse = await client.SendAsync(firstReq);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdOrder = await firstResponse.Content.ReadFromJsonAsync<OrderDto>();
        createdOrder.Should().NotBeNull();
        createdOrder!.TotalAmount.Should().Be(1499.00m);
        createdOrder.Status.Should().Be("Pending");
        orders.Should().HaveCount(1);
        publishMock.Verify(p => p.Publish(It.IsAny<OrderSubmitted>(), It.IsAny<CancellationToken>()), Times.Once);

        // 2. Replay same Idempotency-Key + same payload -> returns cached 201 with identical OrderId without creating 2nd order
        using var replayReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders/checkout")
        {
            Content = JsonContent.Create(checkoutPayload)
        };
        replayReq.Headers.Add("Idempotency-Key", "idem-checkout-001");

        var replayResponse = await client.SendAsync(replayReq);
        replayResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var replayedOrder = await replayResponse.Content.ReadFromJsonAsync<OrderDto>();
        replayedOrder!.Id.Should().Be(createdOrder.Id);
        orders.Should().HaveCount(1);

        // 3. Replay same Idempotency-Key + DIFFERENT payload -> 409 Conflict
        using var conflictReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders/checkout")
        {
            Content = JsonContent.Create(new CheckoutRequest(CreateAddressRequest(), "different_payment_method"))
        };
        conflictReq.Headers.Add("Idempotency-Key", "idem-checkout-001");

        var conflictResponse = await client.SendAsync(conflictReq);
        conflictResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetOrderById_AndCancelOrder_ShouldReturnOrderAndTransitionToCancelled()
    {
        var orderId = Guid.NewGuid();
        var existingOrder = ShopSphere.Services.Order.Domain.Order.Create(
            orderId,
            "cust-123",
            ShopSphere.Services.Order.Domain.ShippingAddress.Create(
                "Jane Doe", "123 Market St", "San Francisco", "CA", "94105", "US", "555-0199"),
            "credit_card",
            [ShopSphere.Services.Order.Domain.OrderItem.Create(orderId, "SKU-1", "Item 1", 2, 25.00m, "USD", 1)]);

        var orders = new List<ShopSphere.Services.Order.Domain.Order> { existingOrder };

        await using var app = await CreateTestAppAsync(orders);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Customer-Id", "cust-123");

        // GET /api/v1/orders/{id}
        var getResponse = await client.GetAsync($"/api/v1/orders/{orderId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await getResponse.Content.ReadFromJsonAsync<OrderDto>();
        fetched!.Id.Should().Be(orderId);
        fetched.TotalAmount.Should().Be(50.00m);

        // POST /api/v1/orders/{id}/cancel
        var cancelResponse = await client.PostAsJsonAsync(
            $"/api/v1/orders/{orderId}/cancel",
            new CancelOrderRequest("Changed my mind"));
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<OrderDto>();
        cancelled!.Status.Should().Be("Cancelled");
        cancelled.CancellationReason.Should().Be("Changed my mind");
    }

    [Fact]
    public async Task Checkout_WithVndPaymentMethod_ShouldConvertUsdCartQuoteToVndUsingHardcodedExchangeRate()
    {
        var orders = new List<ShopSphere.Services.Order.Domain.Order>();
        var idempotencyRecords = new List<IdempotencyRecord>();
        var publishMock = new Mock<IPublishEndpoint>();
        var cartClientMock = new Mock<ICartClient>();

        cartClientMock
            .Setup(c => c.GetCheckoutQuoteAsync("cust-vn", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new CartCheckoutQuoteDto(
                "cust-vn",
                [new CartQuoteItemDto("KB-RGB-001", "Mechanical Keyboard", 2, 100.00m, 200.00m, "USD", 1, true)],
                200.00m,
                "USD",
                new Dictionary<string, int> { ["KB-RGB-001"] = 1 },
                true,
                [])));

        cartClientMock
            .Setup(c => c.ClearCartAsync("cust-vn", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        await using var app = await CreateTestAppAsync(orders, idempotencyRecords, cartClientMock.Object, publishMock.Object);
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Customer-Id", "cust-vn");

        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders/checkout")
        {
            Content = JsonContent.Create(new CheckoutRequest(CreateAddressRequest(), "digital_wallet"))
        };
        req.Headers.Add("Idempotency-Key", "idem-vnd-001");

        var response = await client.SendAsync(req);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var createdOrder = await response.Content.ReadFromJsonAsync<OrderDto>();
        createdOrder.Should().NotBeNull();
        createdOrder!.Currency.Should().Be("VND");
        createdOrder.TotalAmount.Should().Be(5_080_000m); // 200 USD * 25,400 VND
    }

    private static async Task<WebApplication> CreateTestAppAsync(
        List<ShopSphere.Services.Order.Domain.Order>? orders = null,
        List<IdempotencyRecord>? idempotencyRecords = null,
        ICartClient? cartClient = null,
        IPublishEndpoint? publishEndpoint = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        var orderList = orders ?? [];
        var idempotencyList = idempotencyRecords ?? [];

        var ordersMockSet = MockDbSetExtensions.CreateMockDbSet(orderList);
        var idempotencyMockSet = MockDbSetExtensions.CreateMockDbSet(idempotencyList);

        var dbContextMock = new Mock<IOrderDbContext>();
        dbContextMock.Setup(d => d.Orders).Returns(ordersMockSet.Object);
        dbContextMock.Setup(d => d.IdempotencyRecords).Returns(idempotencyMockSet.Object);

        builder.Services.AddSingleton(dbContextMock.Object);
        builder.Services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();
        builder.Services.AddSingleton(cartClient ?? new Mock<ICartClient>().Object);
        builder.Services.AddSingleton(publishEndpoint ?? new Mock<IPublishEndpoint>().Object);
        builder.Services.AddOrderApplication();
        builder.Services.AddHealthChecks();

        var app = builder.Build();
        app.MapOrderEndpoints();
        await app.StartAsync();
        return app;
    }
}

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ShopSphere.Services.Shipping.Api.Endpoints;
using ShopSphere.Services.Shipping.Application.Abstractions;
using ShopSphere.Services.Shipping.Application.Commands;
using ShopSphere.Services.Shipping.Application.DTOs;
using ShopSphere.Services.Shipping.Application.Queries;
using ShopSphere.Services.Shipping.Domain;
using ShopSphere.Services.Shipping.Tests.Unit.Helpers;

namespace ShopSphere.Services.Shipping.Tests.Unit.Api;

public class ShippingApiEndpointTests
{
    [Fact]
    public async Task GetAndLifecycleEndpoints_ReturnExpectedResponses_AndEnforceHandoverCancellationGuard()
    {
        var shipmentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var shipment = Shipment.Create(
            shipmentId,
            orderId,
            "cust-001",
            ShippingAddress.Create("Alice Nguyen", "123 Le Loi", "Ho Chi Minh City", "HC", "700000", "VN", "+84901234567"),
            [ShipmentItem.Create(shipmentId, "SKU-001", "Mechanical Keyboard", 1)]);
        shipment.AssignTrackingLabel("SS-TRK-API-001", "REQ-API-1");

        var store = new List<Shipment> { shipment };
        var mockSet = MockDbSetExtensions.CreateMockDbSet(store);
        var dbMock = new Mock<IShippingDbContext>();
        dbMock.SetupGet(d => d.Shipments).Returns(mockSet.Object);
        dbMock.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton(dbMock.Object);
        builder.Services.AddSingleton(new Mock<IPublishEndpoint>().Object);
        builder.Services.AddScoped<RecordCarrierHandoverCommandHandler>();
        builder.Services.AddScoped<RecordDeliveryCommandHandler>();
        builder.Services.AddScoped<CancelShipmentCommandHandler>();
        builder.Services.AddScoped<GetShipmentQueriesHandler>();

        await using var app = builder.Build();
        app.MapShippingApiEndpoints();
        await app.StartAsync();

        using var client = app.GetTestClient();

        // 1. Get by OrderId
        var byOrderResponse = await client.GetAsync($"/api/v1/shipments/orders/{orderId}");
        byOrderResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var byOrderDto = await byOrderResponse.Content.ReadFromJsonAsync<ShipmentDto>();
        byOrderDto!.TrackingNumber.Should().Be("SS-TRK-API-001");

        // 2. Get by TrackingNumber
        var byTrackingResponse = await client.GetAsync("/api/v1/shipments/tracking/SS-TRK-API-001");
        byTrackingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Record Carrier Handover
        var handoverResponse = await client.PostAsJsonAsync(
            $"/api/v1/shipments/{shipment.Id}/handover",
            new { WarehouseLocation = "WH-SGN-01", Notes = "Handed over to driver" });
        handoverResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var handedOverDto = await handoverResponse.Content.ReadFromJsonAsync<ShipmentDto>();
        handedOverDto!.Status.Should().Be("HandedOver");

        // 4. Cancel after handover returns 409 Conflict
        var cancelResponse = await client.PostAsJsonAsync(
            $"/api/v1/shipments/{shipment.Id}/cancel",
            new { Reason = "Customer changed mind after carrier pickup" });
        cancelResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 5. Deliver shipment
        var deliverResponse = await client.PostAsJsonAsync(
            $"/api/v1/shipments/{shipment.Id}/deliver",
            new { Location = "Ho Chi Minh City", Notes = "Delivered to front desk" });
        deliverResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var deliveredDto = await deliverResponse.Content.ReadFromJsonAsync<ShipmentDto>();
        deliveredDto!.Status.Should().Be("Delivered");

        await app.StopAsync();
    }
}

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShopSphere.Services.Inventory.Application.Common.Interfaces;
using ShopSphere.Services.Inventory.Application.Consumers;
using ShopSphere.Services.Inventory.Domain;
using ShopSphere.Services.Inventory.Tests.Unit.Helpers;
using ShopSphere.Services.Shipping.Application.Contracts;
using Xunit;

namespace ShopSphere.Services.Inventory.Tests.Unit;

public class ShipmentHandedOverConsumerTests
{
    [Fact]
    public async Task HandleShipmentHandedOverAsync_CommitsHeldReservations_AndIsIdempotent()
    {
        var orderId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();
        var stockItem = StockItem.Create("TEST-SKU-HANDOVER", 10);
        var hold = stockItem.HoldStock(orderId, 2, TimeSpan.FromMinutes(30));

        var stockItems = new List<StockItem> { stockItem };
        var reservations = new List<StockReservation> { hold };

        var mockDbContext = new Mock<IInventoryDbContext>();
        mockDbContext.Setup(c => c.StockItems).Returns(MockDbSetExtensions.CreateMockDbSet(stockItems).Object);
        mockDbContext.Setup(c => c.Reservations).Returns(MockDbSetExtensions.CreateMockDbSet(reservations).Object);

        var consumer = new ShipmentHandedOverConsumer(mockDbContext.Object, NullLogger<ShipmentHandedOverConsumer>.Instance);
        var evt = new ShipmentHandedOver(orderId, shipmentId, "ShopSphereExpress", "SS-TRK-100", DateTime.UtcNow);

        await consumer.HandleShipmentHandedOverAsync(evt);

        hold.Status.Should().Be(ReservationStatus.Committed);
        stockItem.QuantityCommitted.Should().Be(2);
        mockDbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Second call is idempotent
        await consumer.HandleShipmentHandedOverAsync(evt);
        mockDbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

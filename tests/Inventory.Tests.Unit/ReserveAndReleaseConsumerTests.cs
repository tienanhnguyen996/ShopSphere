using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Inventory.Application.Common.Interfaces;
using ShopSphere.Services.Inventory.Application.Consumers;
using ShopSphere.Services.Inventory.Application.Contracts;
using ShopSphere.Services.Inventory.Domain;
using ShopSphere.Services.Inventory.Tests.Unit.Helpers;
using Xunit;

namespace ShopSphere.Services.Inventory.Tests.Unit;

public class ReserveAndReleaseConsumerTests
{
    private readonly Mock<IInventoryDbContext> _mockDbContext;
    private readonly Mock<ILogger<ReserveInventoryConsumer>> _mockReserveLogger;
    private readonly Mock<ILogger<ReleaseInventoryConsumer>> _mockReleaseLogger;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;

    public ReserveAndReleaseConsumerTests()
    {
        _mockDbContext = new Mock<IInventoryDbContext>();
        _mockReserveLogger = new Mock<ILogger<ReserveInventoryConsumer>>();
        _mockReleaseLogger = new Mock<ILogger<ReleaseInventoryConsumer>>();
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
    }

    [Fact]
    public async Task ReserveInventory_WhenStockAvailable_ReservesAndPublishesInventoryReserved()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU-1", 10);
        var stockItems = new List<StockItem> { stockItem };
        var reservations = new List<StockReservation>();

        var mockStockItemDbSet = MockDbSetExtensions.CreateMockDbSet(stockItems);
        var mockReservationDbSet = MockDbSetExtensions.CreateMockDbSet(reservations);

        _mockDbContext.Setup(c => c.StockItems).Returns(mockStockItemDbSet.Object);
        _mockDbContext.Setup(c => c.Reservations).Returns(mockReservationDbSet.Object);

        var consumer = new ReserveInventoryConsumer(_mockDbContext.Object, _mockReserveLogger.Object);

        var orderId = Guid.NewGuid();
        var command = new ReserveInventory(
            orderId,
            new[] { new InventoryItemRequirement("TEST-SKU-1", 3) },
            TimeSpan.FromMinutes(10));

        // Act
        await consumer.HandleReservationAsync(command, _mockPublishEndpoint.Object);

        // Assert
        stockItem.QuantityAvailable.Should().Be(7);
        stockItem.QuantityHeld.Should().Be(3);
        stockItem.Reservations.Should().ContainSingle(r => r.OrderId == orderId && r.Quantity == 3);

        _mockDbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        _mockPublishEndpoint.Verify(p => p.Publish(
            It.Is<InventoryReserved>(e => e.OrderId == orderId && e.Items.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReserveInventory_WhenInsufficientStock_PublishesInventoryRejected()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU-1", 2);
        var stockItems = new List<StockItem> { stockItem };
        var reservations = new List<StockReservation>();

        _mockDbContext.Setup(c => c.StockItems).Returns(MockDbSetExtensions.CreateMockDbSet(stockItems).Object);
        _mockDbContext.Setup(c => c.Reservations).Returns(MockDbSetExtensions.CreateMockDbSet(reservations).Object);

        var consumer = new ReserveInventoryConsumer(_mockDbContext.Object, _mockReserveLogger.Object);

        var orderId = Guid.NewGuid();
        var command = new ReserveInventory(
            orderId,
            new[] { new InventoryItemRequirement("TEST-SKU-1", 5) },
            TimeSpan.FromMinutes(10));

        // Act
        await consumer.HandleReservationAsync(command, _mockPublishEndpoint.Object);

        // Assert
        stockItem.QuantityAvailable.Should().Be(2);
        stockItem.QuantityHeld.Should().Be(0);

        _mockPublishEndpoint.Verify(p => p.Publish(
            It.Is<InventoryRejected>(e => e.OrderId == orderId && e.UnavailableSkus.Contains("TEST-SKU-1")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReserveInventory_WhenDuplicateMessage_ReturnsExistingReservation()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var stockItem = StockItem.Create("TEST-SKU-1", 10);
        var existingHold = stockItem.HoldStock(orderId, 4, TimeSpan.FromMinutes(15));
        var stockItems = new List<StockItem> { stockItem };
        var reservations = new List<StockReservation> { existingHold };

        _mockDbContext.Setup(c => c.StockItems).Returns(MockDbSetExtensions.CreateMockDbSet(stockItems).Object);
        _mockDbContext.Setup(c => c.Reservations).Returns(MockDbSetExtensions.CreateMockDbSet(reservations).Object);

        var consumer = new ReserveInventoryConsumer(_mockDbContext.Object, _mockReserveLogger.Object);

        var command = new ReserveInventory(
            orderId,
            new[] { new InventoryItemRequirement("TEST-SKU-1", 4) },
            TimeSpan.FromMinutes(15));

        // Act
        await consumer.HandleReservationAsync(command, _mockPublishEndpoint.Object);

        // Assert - no additional stock deducted
        stockItem.QuantityAvailable.Should().Be(6);
        stockItem.QuantityHeld.Should().Be(4);

        _mockPublishEndpoint.Verify(p => p.Publish(
            It.Is<InventoryReserved>(e => e.OrderId == orderId && e.Items.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReserveInventory_OnDbUpdateConcurrencyException_RetriesAndSucceeds()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU-1", 10);
        var stockItems = new List<StockItem> { stockItem };
        var reservations = new List<StockReservation>();

        _mockDbContext.Setup(c => c.StockItems).Returns(MockDbSetExtensions.CreateMockDbSet(stockItems).Object);
        _mockDbContext.Setup(c => c.Reservations).Returns(MockDbSetExtensions.CreateMockDbSet(reservations).Object);

        var callCount = 0;
        _mockDbContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new DbUpdateConcurrencyException("Concurrency conflict");
                }
                return Task.FromResult(1);
            });

        var consumer = new ReserveInventoryConsumer(_mockDbContext.Object, _mockReserveLogger.Object);

        var orderId = Guid.NewGuid();
        var command = new ReserveInventory(
            orderId,
            new[] { new InventoryItemRequirement("TEST-SKU-1", 2) },
            TimeSpan.FromMinutes(10));

        // Act
        await consumer.HandleReservationAsync(command, _mockPublishEndpoint.Object);

        // Assert
        callCount.Should().Be(2);
        _mockPublishEndpoint.Verify(p => p.Publish(
            It.Is<InventoryReserved>(e => e.OrderId == orderId),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ReleaseInventory_WhenHoldExists_ReleasesStockAndPublishesInventoryReleased()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var stockItem = StockItem.Create("TEST-SKU-1", 10);
        var hold = stockItem.HoldStock(orderId, 3, TimeSpan.FromMinutes(15));

        var stockItems = new List<StockItem> { stockItem };
        var reservations = new List<StockReservation> { hold };

        _mockDbContext.Setup(c => c.StockItems).Returns(MockDbSetExtensions.CreateMockDbSet(stockItems).Object);
        _mockDbContext.Setup(c => c.Reservations).Returns(MockDbSetExtensions.CreateMockDbSet(reservations).Object);

        var consumer = new ReleaseInventoryConsumer(_mockDbContext.Object, _mockReleaseLogger.Object);
        var command = new ReleaseInventory(orderId, "Customer cancelled order");

        // Act
        await consumer.HandleReleaseAsync(command, _mockPublishEndpoint.Object);

        // Assert
        stockItem.QuantityAvailable.Should().Be(10);
        stockItem.QuantityHeld.Should().Be(0);
        hold.Status.Should().Be(ReservationStatus.Released);

        _mockDbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        _mockPublishEndpoint.Verify(p => p.Publish(
            It.Is<InventoryReleased>(e => e.OrderId == orderId && e.Reason == "Customer cancelled order"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseInventory_WhenNoReservations_IsIdempotent()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var reservations = new List<StockReservation>();

        _mockDbContext.Setup(c => c.StockItems).Returns(MockDbSetExtensions.CreateMockDbSet(new List<StockItem>()).Object);
        _mockDbContext.Setup(c => c.Reservations).Returns(MockDbSetExtensions.CreateMockDbSet(reservations).Object);

        var consumer = new ReleaseInventoryConsumer(_mockDbContext.Object, _mockReleaseLogger.Object);
        var command = new ReleaseInventory(orderId, "No reservation test");

        // Act
        await consumer.HandleReleaseAsync(command, _mockPublishEndpoint.Object);

        // Assert
        _mockPublishEndpoint.Verify(p => p.Publish(
            It.Is<InventoryReleased>(e => e.OrderId == orderId),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

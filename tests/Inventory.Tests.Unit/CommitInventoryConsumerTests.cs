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

public class CommitInventoryConsumerTests
{
    private readonly Mock<IInventoryDbContext> _mockDbContext;
    private readonly Mock<ILogger<CommitInventoryConsumer>> _mockLogger;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;

    public CommitInventoryConsumerTests()
    {
        _mockDbContext = new Mock<IInventoryDbContext>();
        _mockLogger = new Mock<ILogger<CommitInventoryConsumer>>();
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
    }

    [Fact]
    public async Task CommitInventory_WhenHeldAndNotExpired_CommitsAndPublishesInventoryCommitted()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var stockItem = StockItem.Create("TEST-SKU-1", 10);
        var hold = stockItem.HoldStock(orderId, 4, TimeSpan.FromMinutes(15));

        var stockItems = new List<StockItem> { stockItem };
        var reservations = new List<StockReservation> { hold };

        _mockDbContext.Setup(c => c.StockItems).Returns(MockDbSetExtensions.CreateMockDbSet(stockItems).Object);
        _mockDbContext.Setup(c => c.Reservations).Returns(MockDbSetExtensions.CreateMockDbSet(reservations).Object);

        var consumer = new CommitInventoryConsumer(_mockDbContext.Object, _mockLogger.Object);
        var command = new CommitInventory(orderId);

        // Act
        await consumer.HandleCommitAsync(command, _mockPublishEndpoint.Object);

        // Assert
        hold.Status.Should().Be(ReservationStatus.Committed);
        stockItem.QuantityAvailable.Should().Be(6);
        stockItem.QuantityHeld.Should().Be(0);
        stockItem.QuantityCommitted.Should().Be(4);

        _mockDbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        _mockPublishEndpoint.Verify(p => p.Publish(
            It.Is<InventoryCommitted>(e => e.OrderId == orderId && e.Items.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommitInventory_WhenAlreadyCommitted_IsIdempotent()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var stockItem = StockItem.Create("TEST-SKU-1", 10);
        var hold = stockItem.HoldStock(orderId, 4, TimeSpan.FromMinutes(15));
        stockItem.CommitHold(hold.Id);

        var stockItems = new List<StockItem> { stockItem };
        var reservations = new List<StockReservation> { hold };

        _mockDbContext.Setup(c => c.StockItems).Returns(MockDbSetExtensions.CreateMockDbSet(stockItems).Object);
        _mockDbContext.Setup(c => c.Reservations).Returns(MockDbSetExtensions.CreateMockDbSet(reservations).Object);

        var consumer = new CommitInventoryConsumer(_mockDbContext.Object, _mockLogger.Object);
        var command = new CommitInventory(orderId);

        // Act
        await consumer.HandleCommitAsync(command, _mockPublishEndpoint.Object);

        // Assert - no additional stock adjustments
        stockItem.QuantityCommitted.Should().Be(4);
        stockItem.QuantityHeld.Should().Be(0);

        _mockPublishEndpoint.Verify(p => p.Publish(
            It.Is<InventoryCommitted>(e => e.OrderId == orderId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommitInventory_WhenExpiredBeforePayment_PublishesInventoryCommitFailedAndRestoresStock()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var stockItem = StockItem.Create("TEST-SKU-1", 10);
        // Expired hold: TTL of -1 minute
        var expiredHold = stockItem.HoldStock(orderId, 3, TimeSpan.FromMinutes(-1));

        var stockItems = new List<StockItem> { stockItem };
        var reservations = new List<StockReservation> { expiredHold };

        _mockDbContext.Setup(c => c.StockItems).Returns(MockDbSetExtensions.CreateMockDbSet(stockItems).Object);
        _mockDbContext.Setup(c => c.Reservations).Returns(MockDbSetExtensions.CreateMockDbSet(reservations).Object);

        var consumer = new CommitInventoryConsumer(_mockDbContext.Object, _mockLogger.Object);
        var command = new CommitInventory(orderId);

        // Act
        await consumer.HandleCommitAsync(command, _mockPublishEndpoint.Object);

        // Assert
        expiredHold.Status.Should().Be(ReservationStatus.Expired);
        stockItem.QuantityAvailable.Should().Be(10); // restored
        stockItem.QuantityHeld.Should().Be(0);
        stockItem.QuantityCommitted.Should().Be(0);

        _mockPublishEndpoint.Verify(p => p.Publish(
            It.Is<InventoryCommitFailed>(e => e.OrderId == orderId && e.Reason.Contains("expired")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommitInventory_WhenNoReservationsFound_PublishesInventoryCommitFailed()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var reservations = new List<StockReservation>();

        _mockDbContext.Setup(c => c.StockItems).Returns(MockDbSetExtensions.CreateMockDbSet(new List<StockItem>()).Object);
        _mockDbContext.Setup(c => c.Reservations).Returns(MockDbSetExtensions.CreateMockDbSet(reservations).Object);

        var consumer = new CommitInventoryConsumer(_mockDbContext.Object, _mockLogger.Object);
        var command = new CommitInventory(orderId);

        // Act
        await consumer.HandleCommitAsync(command, _mockPublishEndpoint.Object);

        // Assert
        _mockPublishEndpoint.Verify(p => p.Publish(
            It.Is<InventoryCommitFailed>(e => e.OrderId == orderId && e.Reason.Contains("No reservations")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

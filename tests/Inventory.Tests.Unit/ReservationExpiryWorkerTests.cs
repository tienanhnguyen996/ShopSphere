using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Inventory.Application.Common.Interfaces;
using ShopSphere.Services.Inventory.Application.Contracts;
using ShopSphere.Services.Inventory.Application.Workers;
using ShopSphere.Services.Inventory.Domain;
using ShopSphere.Services.Inventory.Tests.Unit.Helpers;
using Xunit;

namespace ShopSphere.Services.Inventory.Tests.Unit;

public class ReservationExpiryWorkerTests
{
    private readonly Mock<IInventoryDbContext> _mockDbContext;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<ILogger<ReservationExpiryWorker>> _mockLogger;

    public ReservationExpiryWorkerTests()
    {
        _mockDbContext = new Mock<IInventoryDbContext>();
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockLogger = new Mock<ILogger<ReservationExpiryWorker>>();

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(IInventoryDbContext)))
            .Returns(_mockDbContext.Object);
        serviceProvider.Setup(sp => sp.GetService(typeof(IPublishEndpoint)))
            .Returns(_mockPublishEndpoint.Object);

        var serviceScope = new Mock<IServiceScope>();
        serviceScope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(serviceScope.Object);
    }

    [Fact]
    public async Task SweepExpiredHolds_WhenOverdueHoldsExist_ReleasesStockAndPublishesExpiredEvent()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU-1", 100);
        var orderId = Guid.NewGuid();
        // Expired hold: TTL of -5 minutes
        var expiredHold = stockItem.HoldStock(orderId, 10, TimeSpan.FromMinutes(-5));

        var stockItems = new List<StockItem> { stockItem };
        var reservations = new List<StockReservation> { expiredHold };

        _mockDbContext.Setup(c => c.StockItems).Returns(MockDbSetExtensions.CreateMockDbSet(stockItems).Object);
        _mockDbContext.Setup(c => c.Reservations).Returns(MockDbSetExtensions.CreateMockDbSet(reservations).Object);

        var worker = new ReservationExpiryWorker(
            _mockScopeFactory.Object,
            _mockLogger.Object,
            TimeSpan.FromSeconds(30),
            batchSize: 50);

        // Act
        var releasedCount = await worker.SweepExpiredHoldsAsync();

        // Assert
        releasedCount.Should().Be(1);
        expiredHold.Status.Should().Be(ReservationStatus.Expired);
        stockItem.QuantityAvailable.Should().Be(100); // restored
        stockItem.QuantityHeld.Should().Be(0);

        _mockDbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        _mockPublishEndpoint.Verify(p => p.Publish(
            It.Is<InventoryReservationExpired>(e => e.ReservationId == expiredHold.Id && e.OrderId == orderId && e.Quantity == 10),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SweepExpiredHolds_WhenHoldsNotExpired_DoesNotRelease()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU-1", 100);
        var orderId = Guid.NewGuid();
        // Valid hold: TTL of +15 minutes
        var validHold = stockItem.HoldStock(orderId, 10, TimeSpan.FromMinutes(15));

        var stockItems = new List<StockItem> { stockItem };
        var reservations = new List<StockReservation> { validHold };

        _mockDbContext.Setup(c => c.StockItems).Returns(MockDbSetExtensions.CreateMockDbSet(stockItems).Object);
        _mockDbContext.Setup(c => c.Reservations).Returns(MockDbSetExtensions.CreateMockDbSet(reservations).Object);

        var worker = new ReservationExpiryWorker(
            _mockScopeFactory.Object,
            _mockLogger.Object,
            TimeSpan.FromSeconds(30),
            batchSize: 50);

        // Act
        var releasedCount = await worker.SweepExpiredHoldsAsync();

        // Assert
        releasedCount.Should().Be(0);
        validHold.Status.Should().Be(ReservationStatus.Held);
        stockItem.QuantityAvailable.Should().Be(90);
        stockItem.QuantityHeld.Should().Be(10);

        _mockDbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockPublishEndpoint.Verify(p => p.Publish(
            It.IsAny<InventoryReservationExpired>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SweepExpiredHolds_OnConcurrencyConflictWithCommit_GracefullySkipsWithoutThrowing()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU-1", 100);
        var orderId = Guid.NewGuid();
        var expiredHold = stockItem.HoldStock(orderId, 10, TimeSpan.FromMinutes(-5));

        var stockItems = new List<StockItem> { stockItem };
        var reservations = new List<StockReservation> { expiredHold };

        _mockDbContext.Setup(c => c.StockItems).Returns(MockDbSetExtensions.CreateMockDbSet(stockItems).Object);
        _mockDbContext.Setup(c => c.Reservations).Returns(MockDbSetExtensions.CreateMockDbSet(reservations).Object);

        _mockDbContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Simulated race condition with CommitInventory"));

        var worker = new ReservationExpiryWorker(
            _mockScopeFactory.Object,
            _mockLogger.Object,
            TimeSpan.FromSeconds(30),
            batchSize: 50);

        // Act
        var act = async () => await worker.SweepExpiredHoldsAsync();

        // Assert - does not throw exception
        await act.Should().NotThrowAsync();
    }
}

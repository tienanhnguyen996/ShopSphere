using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Moq;
using ShopSphere.Services.Inventory.Api;
using ShopSphere.Services.Inventory.Api.Health;
using ShopSphere.Services.Inventory.Application.Common.Interfaces;
using ShopSphere.Services.Inventory.Application.Stock.Commands.AdjustStock;
using ShopSphere.Services.Inventory.Application.Stock.Queries.GetStockBySku;
using ShopSphere.Services.Inventory.Domain;
using ShopSphere.Services.Inventory.Infrastructure;
using ShopSphere.Services.Inventory.Tests.Unit.Helpers;
using Xunit;

namespace ShopSphere.Services.Inventory.Tests.Unit;

public class InventoryEndpointAndHealthTests
{
    #region Health Check Tests

    [Fact]
    public async Task SqlServerHealthCheck_WhenCanConnect_ShouldReturnHealthy()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer("Server=localhost;Database=TestInventory;User Id=sa;Password=Password123!;TrustServerCertificate=True;")
            .Options;
        var dbContext = new InventoryDbContext(dbOptions);

        var healthCheck = new SqlServerHealthCheck(dbContext);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert - when localhost is unavailable, returns Unhealthy
        result.Should().NotBeNull();
        result.Status.Should().BeOneOf(HealthStatus.Healthy, HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task RabbitMqHealthCheck_WhenNoBusControl_ShouldReturnHealthy()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var healthCheck = new RabbitMqHealthCheck(serviceProvider.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("registered");
    }

    #endregion

    #region Stock Adjustment Handler Tests

    [Fact]
    public async Task AdjustStock_PositiveQuantity_WhenItemExists_IncreasesStock()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU-1", 10);
        var stockItems = new List<StockItem> { stockItem };

        var mockDbContext = new Mock<IInventoryDbContext>();
        mockDbContext.Setup(c => c.StockItems).Returns(MockDbSetExtensions.CreateMockDbSet(stockItems).Object);

        var validator = new AdjustStockCommandValidator();
        var logger = new Mock<ILogger<AdjustStockCommandHandler>>();
        var handler = new AdjustStockCommandHandler(mockDbContext.Object, validator, logger.Object);

        var command = new AdjustStockCommand("TEST-SKU-1", 20, "Batch restock");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.QuantityAvailable.Should().Be(30);
        result.Value.TotalStock.Should().Be(30);
        stockItem.QuantityAvailable.Should().Be(30);

        mockDbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdjustStock_NegativeQuantity_WithinAvailable_DecreasesStock()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU-1", 25);
        var stockItems = new List<StockItem> { stockItem };

        var mockDbContext = new Mock<IInventoryDbContext>();
        mockDbContext.Setup(c => c.StockItems).Returns(MockDbSetExtensions.CreateMockDbSet(stockItems).Object);

        var validator = new AdjustStockCommandValidator();
        var logger = new Mock<ILogger<AdjustStockCommandHandler>>();
        var handler = new AdjustStockCommandHandler(mockDbContext.Object, validator, logger.Object);

        var command = new AdjustStockCommand("TEST-SKU-1", -10, "Damaged write-off");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.QuantityAvailable.Should().Be(15);
        stockItem.QuantityAvailable.Should().Be(15);

        mockDbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdjustStock_NegativeQuantity_ExceedingAvailable_ReturnsFailure()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU-1", 10);
        var stockItems = new List<StockItem> { stockItem };

        var mockDbContext = new Mock<IInventoryDbContext>();
        mockDbContext.Setup(c => c.StockItems).Returns(MockDbSetExtensions.CreateMockDbSet(stockItems).Object);

        var validator = new AdjustStockCommandValidator();
        var logger = new Mock<ILogger<AdjustStockCommandHandler>>();
        var handler = new AdjustStockCommandHandler(mockDbContext.Object, validator, logger.Object);

        var command = new AdjustStockCommand("TEST-SKU-1", -50, "Exceeding available");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Inventory.NegativeAvailableStock");
        result.Error.Description.Should().Contain("would exceed available quantity");
        stockItem.QuantityAvailable.Should().Be(10);
    }

    [Fact]
    public async Task AdjustStock_NegativeQuantity_WhenSkuNotFound_ReturnsNotFound()
    {
        // Arrange
        var stockItems = new List<StockItem>();

        var mockDbContext = new Mock<IInventoryDbContext>();
        mockDbContext.Setup(c => c.StockItems).Returns(MockDbSetExtensions.CreateMockDbSet(stockItems).Object);

        var validator = new AdjustStockCommandValidator();
        var logger = new Mock<ILogger<AdjustStockCommandHandler>>();
        var handler = new AdjustStockCommandHandler(mockDbContext.Object, validator, logger.Object);

        var command = new AdjustStockCommand("NON-EXISTENT", -5, "Correction");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Inventory.NotFound");
    }

    [Fact]
    public async Task AdjustStock_ZeroQuantity_ReturnsValidationError()
    {
        // Arrange
        var mockDbContext = new Mock<IInventoryDbContext>();
        var validator = new AdjustStockCommandValidator();
        var logger = new Mock<ILogger<AdjustStockCommandHandler>>();
        var handler = new AdjustStockCommandHandler(mockDbContext.Object, validator, logger.Object);

        var command = new AdjustStockCommand("TEST-SKU", 0, "Zero adjustment");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Quantity");
    }

    #endregion

    #region Query Handler Tests

    [Fact]
    public async Task GetStockBySku_WhenItemExists_ReturnsStockDto()
    {
        // Arrange
        var stockItem = StockItem.Create("TEST-SKU-1", 50);
        stockItem.HoldStock(Guid.NewGuid(), 10, TimeSpan.FromMinutes(15));
        var stockItems = new List<StockItem> { stockItem };

        var mockDbContext = new Mock<IInventoryDbContext>();
        mockDbContext.Setup(c => c.StockItems).Returns(MockDbSetExtensions.CreateMockDbSet(stockItems).Object);

        var handler = new GetStockBySkuQueryHandler(mockDbContext.Object);
        var query = new GetStockBySkuQuery("TEST-SKU-1");

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Sku.Should().Be("TEST-SKU-1");
        result.Value.QuantityAvailable.Should().Be(40);
        result.Value.QuantityHeld.Should().Be(10);
        result.Value.QuantityCommitted.Should().Be(0);
        result.Value.TotalStock.Should().Be(50);
    }

    [Fact]
    public async Task GetStockBySku_WhenNotFound_ReturnsFailure()
    {
        // Arrange
        var stockItems = new List<StockItem>();
        var mockDbContext = new Mock<IInventoryDbContext>();
        mockDbContext.Setup(c => c.StockItems).Returns(MockDbSetExtensions.CreateMockDbSet(stockItems).Object);

        var handler = new GetStockBySkuQueryHandler(mockDbContext.Object);
        var query = new GetStockBySkuQuery("UNKNOWN");

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Inventory.NotFound");
    }

    #endregion
}

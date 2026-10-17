using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.Services.Inventory.Domain;
using ShopSphere.Services.Inventory.Infrastructure;
using Xunit;

namespace ShopSphere.Services.Inventory.Tests.Unit;

public class InventoryDbContextTests
{
    private readonly InventoryDbContext _context;

    public InventoryDbContextTests()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer("Server=localhost;Database=TestInventory;User Id=sa;Password=Password123!;TrustServerCertificate=True;")
            .Options;

        _context = new InventoryDbContext(options);
    }

    [Fact]
    public void Model_ShouldUseInventoryDefaultSchema()
    {
        // Assert
        _context.Model.GetDefaultSchema().Should().Be("inventory");
    }

    [Fact]
    public void StockItemEntity_ShouldBeMappedToInventoryStockItemsTable()
    {
        // Arrange
        var entityType = _context.Model.FindEntityType(typeof(StockItem))!;

        // Assert
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("StockItems");
        entityType.GetSchema().Should().Be("inventory");
    }

    [Fact]
    public void StockItemEntity_ShouldHaveUniqueIndexOnSku()
    {
        // Arrange
        var entityType = _context.Model.FindEntityType(typeof(StockItem))!;
        var skuProperty = entityType.FindProperty(nameof(StockItem.Sku))!;

        // Assert
        skuProperty.Should().NotBeNull();
        skuProperty.GetMaxLength().Should().Be(50);
        var index = entityType.FindIndex(skuProperty);
        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void StockItemEntity_ShouldHaveRowVersionAsConcurrencyToken()
    {
        // Arrange
        var entityType = _context.Model.FindEntityType(typeof(StockItem))!;
        var rowVersionProperty = entityType.FindProperty(nameof(StockItem.RowVersion))!;

        // Assert
        rowVersionProperty.Should().NotBeNull();
        rowVersionProperty.IsConcurrencyToken.Should().BeTrue();
        rowVersionProperty.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);
    }

    [Fact]
    public void StockReservationEntity_ShouldBeMappedToReservationsTable()
    {
        // Arrange
        var entityType = _context.Model.FindEntityType(typeof(StockReservation))!;

        // Assert
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("Reservations");
        entityType.GetSchema().Should().Be("inventory");
    }

    [Fact]
    public void StockReservationEntity_ShouldHaveUniqueIndexOnOrderIdAndSku()
    {
        // Arrange
        var entityType = _context.Model.FindEntityType(typeof(StockReservation))!;
        var orderIdProp = entityType.FindProperty(nameof(StockReservation.OrderId))!;
        var skuProp = entityType.FindProperty(nameof(StockReservation.Sku))!;

        // Assert
        var index = entityType.FindIndex(new[] { orderIdProp, skuProp });
        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void StockReservationEntity_ShouldHaveIndexOnStatusAndExpiresAtUtc()
    {
        // Arrange
        var entityType = _context.Model.FindEntityType(typeof(StockReservation))!;
        var statusProp = entityType.FindProperty(nameof(StockReservation.Status))!;
        var expiresProp = entityType.FindProperty(nameof(StockReservation.ExpiresAtUtc))!;

        // Assert
        var index = entityType.FindIndex(new[] { statusProp, expiresProp });
        index.Should().NotBeNull();
        index!.IsUnique.Should().BeFalse();
    }

    [Fact]
    public void StockItem_ShouldCascadeDeleteReservations()
    {
        // Arrange
        var stockItemEntity = _context.Model.FindEntityType(typeof(StockItem))!;
        var reservationEntity = _context.Model.FindEntityType(typeof(StockReservation))!;

        // Assert
        var foreignKey = reservationEntity.GetForeignKeys()
            .FirstOrDefault(fk => fk.PrincipalEntityType == stockItemEntity);

        foreignKey.Should().NotBeNull();
        foreignKey!.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public void OutboxMessages_ShouldBeMappedWithAppropriateSchemaAndIndex()
    {
        // Arrange
        var entityType = _context.Model.FindEntityType(typeof(OutboxMessage))!;

        // Assert
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("OutboxMessages");
        entityType.GetSchema().Should().Be("inventory");

        var processedAtProp = entityType.FindProperty(nameof(OutboxMessage.ProcessedAtUtc))!;
        var index = entityType.FindIndex(processedAtProp);
        index.Should().NotBeNull();
    }

    [Fact]
    public void Model_ShouldIncludeMassTransitOutboxEntities()
    {
        // Arrange
        var inboxState = _context.Model.FindEntityType("MassTransit.EntityFrameworkCoreIntegration.InboxState");
        var outboxMessage = _context.Model.FindEntityType("MassTransit.EntityFrameworkCoreIntegration.OutboxMessage");
        var outboxState = _context.Model.FindEntityType("MassTransit.EntityFrameworkCoreIntegration.OutboxState");

        // Assert
        inboxState.Should().NotBeNull();
        outboxMessage.Should().NotBeNull();
        outboxState.Should().NotBeNull();
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.Services.Shipping.Domain;
using ShopSphere.Services.Shipping.Infrastructure.Persistence;

namespace ShopSphere.Services.Shipping.Tests.Unit.Infrastructure;

public class ShippingDbContextTests
{
    private readonly ShippingDbContext _context;

    public ShippingDbContextTests()
    {
        var options = new DbContextOptionsBuilder<ShippingDbContext>()
            .UseSqlServer("Server=localhost;Database=TestShipping;User Id=sa;Password=DevPassword123!;TrustServerCertificate=True;")
            .Options;

        _context = new ShippingDbContext(options);
    }

    [Fact]
    public void Model_ShouldUseShippingDefaultSchema()
    {
        _context.Model.GetDefaultSchema().Should().Be("shipping");
    }

    [Fact]
    public void ShipmentEntity_ShouldBeMappedToShipmentsTableWithConcurrencyTokenAndUniqueOrderIdIndex()
    {
        var entityType = _context.Model.FindEntityType(typeof(Shipment))!;
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("Shipments");
        entityType.GetSchema().Should().Be("shipping");

        var rowVersionProperty = entityType.FindProperty(nameof(Shipment.RowVersion))!;
        rowVersionProperty.Should().NotBeNull();
        rowVersionProperty.IsConcurrencyToken.Should().BeTrue();

        var orderIdProperty = entityType.FindProperty(nameof(Shipment.OrderId))!;
        var uniqueIndex = entityType.FindIndex(orderIdProperty)!;
        uniqueIndex.Should().NotBeNull();
        uniqueIndex.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void ShipmentItemAndTrackingMilestoneAndOutboxEntities_ShouldBeMappedToShippingSchema()
    {
        var itemType = _context.Model.FindEntityType(typeof(ShipmentItem))!;
        itemType.GetTableName().Should().Be("ShipmentItems");
        itemType.GetSchema().Should().Be("shipping");

        var milestoneType = _context.Model.FindEntityType(typeof(TrackingMilestone))!;
        milestoneType.GetTableName().Should().Be("TrackingMilestones");
        milestoneType.GetSchema().Should().Be("shipping");

        var outboxType = _context.Model.FindEntityType(typeof(OutboxMessage))!;
        outboxType.GetTableName().Should().Be("OutboxMessages");
        outboxType.GetSchema().Should().Be("shipping");
    }
}

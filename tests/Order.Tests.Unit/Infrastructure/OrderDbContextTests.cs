using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Idempotency;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.Services.Order.Application.Saga;
using ShopSphere.Services.Order.Domain;
using ShopSphere.Services.Order.Infrastructure;
using Xunit;

namespace ShopSphere.Services.Order.Tests.Unit.Infrastructure;

public class OrderDbContextTests
{
    private readonly OrderDbContext _context;

    public OrderDbContextTests()
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>()
            .UseSqlServer("Server=localhost;Database=TestOrder;User Id=sa;Password=DevPassword123!;TrustServerCertificate=True;")
            .Options;

        _context = new OrderDbContext(options);
    }

    [Fact]
    public void Model_ShouldUseOrderDefaultSchema()
    {
        _context.Model.GetDefaultSchema().Should().Be("order");
    }

    [Fact]
    public void OrderEntity_ShouldBeMappedToOrdersTableWithConcurrencyToken()
    {
        var entityType = _context.Model.FindEntityType(typeof(ShopSphere.Services.Order.Domain.Order))!;
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("Orders");
        entityType.GetSchema().Should().Be("order");

        var rowVersionProperty = entityType.FindProperty(nameof(ShopSphere.Services.Order.Domain.Order.RowVersion))!;
        rowVersionProperty.Should().NotBeNull();
        rowVersionProperty.IsConcurrencyToken.Should().BeTrue();
    }

    [Fact]
    public void OrderItemEntity_ShouldBeMappedToOrderItemsTable()
    {
        var entityType = _context.Model.FindEntityType(typeof(OrderItem))!;
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("OrderItems");
        entityType.GetSchema().Should().Be("order");
    }

    [Fact]
    public void CheckoutSagaState_ShouldBeMappedToCheckoutSagaStatesTableWithConcurrencyToken()
    {
        var entityType = _context.Model.FindEntityType(typeof(CheckoutSagaState))!;
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("CheckoutSagaStates");
        entityType.GetSchema().Should().Be("order");

        var rowVersionProperty = entityType.FindProperty(nameof(CheckoutSagaState.RowVersion))!;
        rowVersionProperty.Should().NotBeNull();
        rowVersionProperty.IsConcurrencyToken.Should().BeTrue();
    }

    [Fact]
    public void IdempotencyRecordEntity_ShouldBeMappedToIdempotencyRecordsTable()
    {
        var entityType = _context.Model.FindEntityType(typeof(IdempotencyRecord))!;
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("IdempotencyRecords");
        entityType.GetSchema().Should().Be("order");
    }

    [Fact]
    public void OutboxMessageEntity_ShouldBeMappedToOutboxMessagesTable()
    {
        var entityType = _context.Model.FindEntityType(typeof(OutboxMessage))!;
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("OutboxMessages");
        entityType.GetSchema().Should().Be("order");
    }
}

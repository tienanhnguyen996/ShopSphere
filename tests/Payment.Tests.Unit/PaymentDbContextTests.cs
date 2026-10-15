using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.Services.Payment.Domain;
using ShopSphere.Services.Payment.Infrastructure;
using Xunit;

namespace ShopSphere.Services.Payment.Tests.Unit;

public class PaymentDbContextTests
{
    private readonly PaymentDbContext _context;

    public PaymentDbContextTests()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer("Server=localhost;Database=TestPayment;User Id=sa;Password=Password123!;TrustServerCertificate=True;")
            .Options;

        _context = new PaymentDbContext(options);
    }

    [Fact]
    public void Model_ShouldUsePaymentDefaultSchema()
    {
        // Assert
        _context.Model.GetDefaultSchema().Should().Be("payment");
    }

    [Fact]
    public void PaymentAttemptEntity_ShouldBeMappedToPaymentAttemptsTable()
    {
        // Arrange
        var entityType = _context.Model.FindEntityType(typeof(PaymentAttempt))!;

        // Assert
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("PaymentAttempts");
        entityType.GetSchema().Should().Be("payment");
    }

    [Fact]
    public void PaymentAttemptEntity_ShouldHaveIndexedOrderId()
    {
        // Arrange
        var entityType = _context.Model.FindEntityType(typeof(PaymentAttempt))!;
        var orderIdProperty = entityType.FindProperty(nameof(PaymentAttempt.OrderId))!;

        // Assert
        orderIdProperty.Should().NotBeNull();
        var index = entityType.FindIndex(orderIdProperty);
        index.Should().NotBeNull();
    }

    [Fact]
    public void PaymentAttemptEntity_ShouldHaveRowVersionAsConcurrencyToken()
    {
        // Arrange
        var entityType = _context.Model.FindEntityType(typeof(PaymentAttempt))!;
        var rowVersionProperty = entityType.FindProperty(nameof(PaymentAttempt.RowVersion))!;

        // Assert
        rowVersionProperty.Should().NotBeNull();
        rowVersionProperty.IsConcurrencyToken.Should().BeTrue();
        rowVersionProperty.ValueGenerated.Should().Be(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate);
    }

    [Fact]
    public void WebhookLogEntity_ShouldBeMappedToWebhookLogsTableWithUniqueEventId()
    {
        // Arrange
        var entityType = _context.Model.FindEntityType(typeof(PaymentWebhookLog))!;
        var eventIdProperty = entityType.FindProperty(nameof(PaymentWebhookLog.EventId))!;

        // Assert
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("WebhookLogs");
        entityType.GetSchema().Should().Be("payment");

        eventIdProperty.Should().NotBeNull();
        var index = entityType.FindIndex(eventIdProperty);
        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void OutboxMessageEntity_ShouldBeMappedToOutboxMessagesTable()
    {
        // Arrange
        var entityType = _context.Model.FindEntityType(typeof(OutboxMessage))!;

        // Assert
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("OutboxMessages");
        entityType.GetSchema().Should().Be("payment");
    }
}

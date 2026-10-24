using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.Services.Notification.Domain;
using ShopSphere.Services.Notification.Infrastructure.Persistence;

namespace ShopSphere.Services.Notification.Tests.Unit.Infrastructure;

public class NotificationDbContextTests
{
    private readonly NotificationDbContext _context;

    public NotificationDbContextTests()
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseSqlServer("Server=localhost;Database=TestNotification;User Id=sa;Password=DevPassword123!;TrustServerCertificate=True;")
            .Options;

        _context = new NotificationDbContext(options);
    }

    [Fact]
    public void Model_ShouldUseNotificationDefaultSchema_AndMapNotificationJobsWithConcurrencyTokenAndUniqueCompositeIndex()
    {
        _context.Model.GetDefaultSchema().Should().Be("notification");

        var entityType = _context.Model.FindEntityType(typeof(NotificationJob))!;
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("NotificationJobs");
        entityType.GetSchema().Should().Be("notification");

        var rowVersionProperty = entityType.FindProperty(nameof(NotificationJob.RowVersion))!;
        rowVersionProperty.IsConcurrencyToken.Should().BeTrue();

        var compositeIndex = entityType.GetIndexes().FirstOrDefault(i => i.Properties.Count == 3 && i.IsUnique);
        compositeIndex.Should().NotBeNull();

        var outboxType = _context.Model.FindEntityType(typeof(OutboxMessage))!;
        outboxType.GetTableName().Should().Be("OutboxMessages");
        outboxType.GetSchema().Should().Be("notification");
    }
}

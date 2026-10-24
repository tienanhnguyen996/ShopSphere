using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShopSphere.Services.Notification.Application.Abstractions;
using ShopSphere.Services.Notification.Application.Consumers;
using ShopSphere.Services.Notification.Domain;
using ShopSphere.Services.Notification.Infrastructure.Senders;
using ShopSphere.Services.Notification.Tests.Unit.Helpers;
using ShopSphere.Services.Order.Application.Contracts;

namespace ShopSphere.Services.Notification.Tests.Unit.Application;

public class OrderNotificationConsumerTests
{
    private static (Mock<INotificationDbContext> DbMock, List<NotificationJob> Store) CreateMockDbContext()
    {
        var store = new List<NotificationJob>();
        var mockSet = MockDbSetExtensions.CreateMockDbSet(store);
        var dbMock = new Mock<INotificationDbContext>();
        dbMock.SetupGet(d => d.NotificationJobs).Returns(mockSet.Object);
        dbMock.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return (dbMock, store);
    }

    [Fact]
    public async Task Consume_OrderConfirmed_DispatchesEmailAndSms_AndDeduplicatesDuplicateEvents()
    {
        var (dbMock, store) = CreateMockDbContext();
        var sender = new MockNotificationSender();
        var consumer = new OrderNotificationConsumer(dbMock.Object, sender, NullLogger<OrderNotificationConsumer>.Instance);

        var orderId = Guid.NewGuid();
        var evt = new OrderConfirmed(orderId, "cust-100", 199.99m, "USD", Guid.NewGuid(), "SS-TRK-123");

        var contextMock = new Mock<ConsumeContext<OrderConfirmed>>();
        contextMock.SetupGet(c => c.Message).Returns(evt);
        contextMock.SetupGet(c => c.MessageId).Returns(Guid.NewGuid());
        contextMock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await consumer.Consume(contextMock.Object);

        store.Should().HaveCount(2);
        store.Should().OnlyContain(j => j.Status == NotificationStatus.Sent);
        store.Select(j => j.Channel).Should().BeEquivalentTo([NotificationChannel.Email, NotificationChannel.Sms]);

        // Duplicate delivery of OrderConfirmed must not create duplicate jobs or re-send
        await consumer.Consume(contextMock.Object);
        store.Should().HaveCount(2);
    }

    [Fact]
    public async Task Consume_OrderCancelled_WhenProviderFails_RecordsFailedJobWithoutThrowingOrBlocking()
    {
        var (dbMock, store) = CreateMockDbContext();
        var sender = new MockNotificationSender();
        var consumer = new OrderNotificationConsumer(dbMock.Object, sender, NullLogger<OrderNotificationConsumer>.Instance);

        var orderId = Guid.NewGuid();
        var evt = new OrderCancelled(orderId, "fail-notify-cust", "Out of stock");

        var contextMock = new Mock<ConsumeContext<OrderCancelled>>();
        contextMock.SetupGet(c => c.Message).Returns(evt);
        contextMock.SetupGet(c => c.MessageId).Returns(Guid.NewGuid());
        contextMock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        // Must not throw even when provider fails
        await consumer.Consume(contextMock.Object);

        store.Should().ContainSingle();
        store[0].Status.Should().Be(NotificationStatus.Failed);
        store[0].RetryCount.Should().Be(1);
        store[0].LastError.Should().Contain("Simulated Email provider rejection");
    }
}

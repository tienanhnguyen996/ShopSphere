using FluentAssertions;
using ShopSphere.Services.Notification.Domain;

namespace ShopSphere.Services.Notification.Tests.Unit.Domain;

public class NotificationJobTests
{
    [Fact]
    public void Create_WithValidInput_InitializesPendingNotificationJob()
    {
        var orderId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        var job = NotificationJob.Create(
            orderId,
            "cust-001",
            NotificationType.OrderConfirmed,
            NotificationChannel.Email,
            "cust-001@customers.shopsphere.local",
            "Order Confirmed",
            "Your order has been confirmed.",
            messageId);

        job.OrderId.Should().Be(orderId);
        job.CustomerId.Should().Be("cust-001");
        job.Status.Should().Be(NotificationStatus.Pending);
        job.RetryCount.Should().Be(0);
    }

    [Fact]
    public void RecordAttemptFailure_Then_MarkSent_TransitionsToSentAndClearsError()
    {
        var job = NotificationJob.Create(
            Guid.NewGuid(),
            "cust-001",
            NotificationType.OrderConfirmed,
            NotificationChannel.Email,
            "cust-001@customers.shopsphere.local",
            "Order Confirmed",
            "Your order has been confirmed.",
            Guid.NewGuid());

        job.RecordAttemptFailure("SMTP gateway timeout");

        job.Status.Should().Be(NotificationStatus.Failed);
        job.RetryCount.Should().Be(1);
        job.LastError.Should().Be("SMTP gateway timeout");

        job.MarkSent("MSG-EMAIL-001");

        job.Status.Should().Be(NotificationStatus.Sent);
        job.ProviderReferenceId.Should().Be("MSG-EMAIL-001");
        job.LastError.Should().BeNull();
        job.SentAtUtc.Should().NotBeNull();
    }
}

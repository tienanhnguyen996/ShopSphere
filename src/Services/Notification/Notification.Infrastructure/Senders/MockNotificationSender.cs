using ShopSphere.Services.Notification.Application.Abstractions;
using ShopSphere.Services.Notification.Domain;

namespace ShopSphere.Services.Notification.Infrastructure.Senders;

public sealed class MockNotificationSender : INotificationSender
{
    public Task<NotificationDispatchResult> SendAsync(
        NotificationChannel channel,
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (recipient.Contains("fail-notify", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new NotificationDispatchResult(
                false,
                null,
                $"Simulated {channel} provider rejection for recipient '{recipient}'."));
        }

        return Task.FromResult(new NotificationDispatchResult(
            true,
            $" MOCK-{channel.ToString().ToUpperInvariant()}-{Guid.NewGuid():N}"[..24].Trim(),
            null));
    }
}

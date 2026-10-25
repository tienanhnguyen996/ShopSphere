using ShopSphere.Services.Notification.Domain;

namespace ShopSphere.Services.Notification.Application.Abstractions;

public sealed record NotificationDispatchResult(
    bool Succeeded,
    string? ProviderReferenceId,
    string? ErrorMessage);

public interface INotificationSender
{
    Task<NotificationDispatchResult> SendAsync(
        NotificationChannel channel,
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken = default);
}

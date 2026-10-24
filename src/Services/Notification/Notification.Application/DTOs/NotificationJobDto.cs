using ShopSphere.Services.Notification.Domain;

namespace ShopSphere.Services.Notification.Application.DTOs;

public sealed record NotificationJobDto(
    Guid Id,
    Guid OrderId,
    string CustomerId,
    string NotificationType,
    string Channel,
    string Recipient,
    string Subject,
    string Body,
    string Status,
    Guid SourceMessageId,
    int RetryCount,
    string? ProviderReferenceId,
    string? LastError,
    DateTime CreatedAtUtc,
    DateTime? SentAtUtc,
    DateTime? UpdatedAtUtc)
{
    public static NotificationJobDto FromDomain(NotificationJob job) =>
        new(
            job.Id,
            job.OrderId,
            job.CustomerId,
            job.NotificationType.ToString(),
            job.Channel.ToString(),
            job.Recipient,
            job.Subject,
            job.Body,
            job.Status.ToString(),
            job.SourceMessageId,
            job.RetryCount,
            job.ProviderReferenceId,
            job.LastError,
            job.CreatedAtUtc,
            job.SentAtUtc,
            job.UpdatedAtUtc);
}

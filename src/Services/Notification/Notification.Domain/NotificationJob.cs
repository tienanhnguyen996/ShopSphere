using ShopSphere.BuildingBlocks.Common.Domain;
using ShopSphere.Services.Notification.Domain.Exceptions;

namespace ShopSphere.Services.Notification.Domain;

public class NotificationJob : AggregateRoot<Guid>, IVersionedEntity, IAuditableEntity
{
    public Guid OrderId { get; private set; }
    public string CustomerId { get; private set; } = string.Empty;
    public NotificationType NotificationType { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public string Recipient { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public NotificationStatus Status { get; private set; }
    public Guid SourceMessageId { get; private set; }
    public int RetryCount { get; private set; }
    public string? ProviderReferenceId { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; private set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    private NotificationJob() { }

    private NotificationJob(
        Guid id,
        Guid orderId,
        string customerId,
        NotificationType notificationType,
        NotificationChannel channel,
        string recipient,
        string subject,
        string body,
        Guid sourceMessageId) : base(id)
    {
        if (orderId == Guid.Empty)
        {
            throw new NotificationDomainException("OrderId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new NotificationDomainException("CustomerId is required.");
        }

        if (string.IsNullOrWhiteSpace(recipient))
        {
            throw new NotificationDomainException("Recipient is required.");
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new NotificationDomainException("Subject is required.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new NotificationDomainException("Body is required.");
        }

        OrderId = orderId;
        CustomerId = customerId.Trim();
        NotificationType = notificationType;
        Channel = channel;
        Recipient = recipient.Trim();
        Subject = subject.Trim();
        Body = body.Trim();
        SourceMessageId = sourceMessageId;
        Status = NotificationStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static NotificationJob Create(
        Guid orderId,
        string customerId,
        NotificationType notificationType,
        NotificationChannel channel,
        string recipient,
        string subject,
        string body,
        Guid sourceMessageId)
    {
        return new NotificationJob(
            Guid.NewGuid(),
            orderId,
            customerId,
            notificationType,
            channel,
            recipient,
            subject,
            body,
            sourceMessageId);
    }

    public void MarkSent(string providerReferenceId)
    {
        if (string.IsNullOrWhiteSpace(providerReferenceId))
        {
            throw new NotificationDomainException("Provider reference ID is required when marking notification as sent.");
        }

        if (Status == NotificationStatus.Sent)
        {
            return;
        }

        var now = DateTime.UtcNow;
        ProviderReferenceId = providerReferenceId.Trim();
        LastError = null;
        Status = NotificationStatus.Sent;
        SentAtUtc = now;
        UpdatedAtUtc = now;
    }

    public void RecordAttemptFailure(string errorReason)
    {
        if (Status == NotificationStatus.Sent)
        {
            return;
        }

        RetryCount++;
        LastError = string.IsNullOrWhiteSpace(errorReason)
            ? "Notification dispatch failed."
            : errorReason.Trim();
        Status = NotificationStatus.Failed;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

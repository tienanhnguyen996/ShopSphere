namespace ShopSphere.Services.Notification.Domain.Exceptions;

public class NotificationDomainException : Exception
{
    public NotificationDomainException(string message) : base(message) { }
}

public class NotificationJobNotFoundException : NotificationDomainException
{
    public Guid NotificationJobId { get; }

    public NotificationJobNotFoundException(Guid notificationJobId)
        : base($"Notification job '{notificationJobId}' was not found.")
    {
        NotificationJobId = notificationJobId;
    }
}

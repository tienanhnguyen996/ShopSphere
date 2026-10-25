namespace ShopSphere.Services.Notification.Domain;

public enum NotificationChannel
{
    Email = 1,
    Sms = 2
}

public enum NotificationType
{
    OrderConfirmed = 1,
    OrderCancelled = 2
}

public enum NotificationStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3
}

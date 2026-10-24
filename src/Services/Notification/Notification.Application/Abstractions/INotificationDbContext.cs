using Microsoft.EntityFrameworkCore;
using ShopSphere.Services.Notification.Domain;

namespace ShopSphere.Services.Notification.Application.Abstractions;

public interface INotificationDbContext
{
    DbSet<NotificationJob> NotificationJobs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

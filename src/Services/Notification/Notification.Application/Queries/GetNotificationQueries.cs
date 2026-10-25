using Microsoft.EntityFrameworkCore;
using ShopSphere.Services.Notification.Application.Abstractions;
using ShopSphere.Services.Notification.Application.DTOs;

namespace ShopSphere.Services.Notification.Application.Queries;

public sealed record GetNotificationByIdQuery(Guid NotificationJobId);
public sealed record GetNotificationsByOrderIdQuery(Guid OrderId);

public sealed class GetNotificationQueriesHandler(INotificationDbContext dbContext)
{
    public async Task<NotificationJobDto?> HandleAsync(
        GetNotificationByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var job = await dbContext.NotificationJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == query.NotificationJobId, cancellationToken);

        return job is null ? null : NotificationJobDto.FromDomain(job);
    }

    public async Task<IReadOnlyList<NotificationJobDto>> HandleAsync(
        GetNotificationsByOrderIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var jobs = await dbContext.NotificationJobs
            .AsNoTracking()
            .Where(j => j.OrderId == query.OrderId)
            .OrderByDescending(j => j.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return jobs.Select(NotificationJobDto.FromDomain).ToList();
    }
}

using Microsoft.EntityFrameworkCore;
using ShopSphere.Services.Notification.Application.Abstractions;
using ShopSphere.Services.Notification.Application.DTOs;
using ShopSphere.Services.Notification.Domain;
using ShopSphere.Services.Notification.Domain.Exceptions;

namespace ShopSphere.Services.Notification.Application.Commands;

public sealed record RetryNotificationCommand(Guid NotificationJobId);

public sealed class RetryNotificationCommandHandler(
    INotificationDbContext dbContext,
    INotificationSender notificationSender)
{
    public async Task<NotificationJobDto> HandleAsync(
        RetryNotificationCommand command,
        CancellationToken cancellationToken = default)
    {
        var job = await dbContext.NotificationJobs
            .FirstOrDefaultAsync(j => j.Id == command.NotificationJobId, cancellationToken)
            ?? throw new NotificationJobNotFoundException(command.NotificationJobId);

        if (job.Status == NotificationStatus.Sent)
        {
            return NotificationJobDto.FromDomain(job);
        }

        var result = await notificationSender.SendAsync(
            job.Channel,
            job.Recipient,
            job.Subject,
            job.Body,
            cancellationToken);

        if (result.Succeeded)
        {
            job.MarkSent(result.ProviderReferenceId ?? $"MSG-{Guid.NewGuid():N}");
        }
        else
        {
            job.RecordAttemptFailure(result.ErrorMessage ?? "Manual retry failed.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return NotificationJobDto.FromDomain(job);
    }
}

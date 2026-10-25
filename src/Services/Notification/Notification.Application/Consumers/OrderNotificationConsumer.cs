using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Notification.Application.Abstractions;
using ShopSphere.Services.Notification.Domain;
using ShopSphere.Services.Order.Application.Contracts;

namespace ShopSphere.Services.Notification.Application.Consumers;

public sealed class OrderNotificationConsumer(
    INotificationDbContext dbContext,
    INotificationSender notificationSender,
    ILogger<OrderNotificationConsumer> logger) :
    IConsumer<OrderConfirmed>,
    IConsumer<MessageEnvelope<OrderConfirmed>>,
    IConsumer<OrderCancelled>,
    IConsumer<MessageEnvelope<OrderCancelled>>
{
    public Task Consume(ConsumeContext<OrderConfirmed> context) =>
        HandleOrderConfirmedAsync(
            context.Message,
            context.MessageId ?? Guid.NewGuid(),
            context.CancellationToken);

    public Task Consume(ConsumeContext<MessageEnvelope<OrderConfirmed>> context) =>
        HandleOrderConfirmedAsync(
            context.Message.Payload,
            context.Message.MessageId,
            context.CancellationToken);

    public Task Consume(ConsumeContext<OrderCancelled> context) =>
        HandleOrderCancelledAsync(
            context.Message,
            context.MessageId ?? Guid.NewGuid(),
            context.CancellationToken);

    public Task Consume(ConsumeContext<MessageEnvelope<OrderCancelled>> context) =>
        HandleOrderCancelledAsync(
            context.Message.Payload,
            context.Message.MessageId,
            context.CancellationToken);

    public async Task HandleOrderConfirmedAsync(
        OrderConfirmed evt,
        Guid sourceMessageId,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Order {evt.OrderId:N} Confirmed";
        var body = $"Your order {evt.OrderId} for {evt.TotalAmount:F2} {evt.Currency} has been confirmed. Tracking Number: {evt.TrackingNumber}.";

        await DispatchChannelNotificationAsync(
            evt.OrderId,
            evt.CustomerId,
            NotificationType.OrderConfirmed,
            NotificationChannel.Email,
            $"{evt.CustomerId}@customers.shopsphere.local",
            subject,
            body,
            sourceMessageId,
            cancellationToken);

        await DispatchChannelNotificationAsync(
            evt.OrderId,
            evt.CustomerId,
            NotificationType.OrderConfirmed,
            NotificationChannel.Sms,
            $"sms:{evt.CustomerId}",
            subject,
            body,
            sourceMessageId,
            cancellationToken);
    }

    public async Task HandleOrderCancelledAsync(
        OrderCancelled evt,
        Guid sourceMessageId,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Order {evt.OrderId:N} Cancelled";
        var body = $"Your order {evt.OrderId} was cancelled. Reason: {evt.Reason}.";

        await DispatchChannelNotificationAsync(
            evt.OrderId,
            evt.CustomerId,
            NotificationType.OrderCancelled,
            NotificationChannel.Email,
            $"{evt.CustomerId}@customers.shopsphere.local",
            subject,
            body,
            sourceMessageId,
            cancellationToken);
    }

    private async Task DispatchChannelNotificationAsync(
        Guid orderId,
        string customerId,
        NotificationType notificationType,
        NotificationChannel channel,
        string recipient,
        string subject,
        string body,
        Guid sourceMessageId,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.NotificationJobs
            .FirstOrDefaultAsync(
                j => j.OrderId == orderId && j.NotificationType == notificationType && j.Channel == channel,
                cancellationToken);

        if (existing is not null && existing.Status == NotificationStatus.Sent)
        {
            logger.LogInformation(
                "Idempotent skip: {NotificationType} ({Channel}) already sent for Order {OrderId}",
                notificationType,
                channel,
                orderId);
            return;
        }

        var job = existing ?? NotificationJob.Create(
            orderId,
            customerId,
            notificationType,
            channel,
            recipient,
            subject,
            body,
            sourceMessageId);

        if (existing is null)
        {
            dbContext.NotificationJobs.Add(job);
        }

        // Rule: Notification failure must NEVER block or roll back checkout/order processing.
        try
        {
            var result = await notificationSender.SendAsync(channel, recipient, subject, body, cancellationToken);
            if (result.Succeeded)
            {
                job.MarkSent(result.ProviderReferenceId ?? $"MSG-{Guid.NewGuid():N}");
            }
            else
            {
                job.RecordAttemptFailure(result.ErrorMessage ?? "Notification provider returned failure.");
                logger.LogWarning(
                    "Notification dispatch failed for Order {OrderId} ({Channel}): {Error}",
                    orderId,
                    channel,
                    result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            job.RecordAttemptFailure(ex.Message);
            logger.LogWarning(
                ex,
                "Non-blocking exception while sending {NotificationType} ({Channel}) for Order {OrderId}",
                notificationType,
                channel,
                orderId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

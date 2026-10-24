using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopSphere.Services.Inventory.Application.Contracts;
using ShopSphere.Services.Order.Application.Common.Interfaces;
using ShopSphere.Services.Order.Application.Contracts;
using ShopSphere.Services.Order.Domain;
using ShopSphere.Services.Payment.Application.Contracts;
using ShopSphere.Services.Shipping.Application.Contracts;

namespace ShopSphere.Services.Order.Application.Saga;

/// <summary>
/// Synchronizes the Order aggregate root in order.Orders with Checkout Saga lifecycle events.
/// Enforces optimistic concurrency retry and late-event guards.
/// </summary>
public class OrderStateSyncConsumer :
    IConsumer<OrderSubmitted>,
    IConsumer<InventoryReserved>,
    IConsumer<PaymentPendingResolution>,
    IConsumer<PaymentSucceeded>,
    IConsumer<InventoryCommitFailed>,
    IConsumer<ShipmentFailed>,
    IConsumer<OrderConfirmed>,
    IConsumer<OrderCancelled>
{
    private readonly IOrderDbContext _dbContext;
    private readonly ILogger<OrderStateSyncConsumer> _logger;

    public OrderStateSyncConsumer(
        IOrderDbContext dbContext,
        ILogger<OrderStateSyncConsumer> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task Consume(ConsumeContext<OrderSubmitted> context) =>
        UpdateOrderWithRetryAsync(context.Message.OrderId, order =>
        {
            if (order.Status == OrderStatus.Pending)
            {
                order.MarkReserving();
            }
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<InventoryReserved> context) =>
        UpdateOrderWithRetryAsync(context.Message.OrderId, order =>
        {
            if (order.Status is OrderStatus.Pending or OrderStatus.Reserving)
            {
                order.MarkPaying();
            }
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<PaymentPendingResolution> context) =>
        UpdateOrderWithRetryAsync(context.Message.OrderId, order =>
        {
            if (order.Status is OrderStatus.Pending or OrderStatus.Reserving or OrderStatus.Paying or OrderStatus.PaymentUnknown)
            {
                order.MarkPaymentUnknown(context.Message.PaymentAttemptId, context.Message.Reason);
            }
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<PaymentSucceeded> context) =>
        UpdateOrderWithRetryAsync(context.Message.OrderId, order =>
        {
            if (order.Status == OrderStatus.Cancelled)
            {
                // Late payment on cancelled order -> transition to Refunding!
                order.MarkRefunding(
                    "Late payment succeeded on already cancelled order; initiating refund.",
                    context.Message.PaymentAttemptId);
                return;
            }

            if (order.Status is OrderStatus.Pending or OrderStatus.Reserving or OrderStatus.Paying or OrderStatus.PaymentUnknown)
            {
                order.MarkPaymentConfirmed(context.Message.PaymentAttemptId);
            }
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<InventoryCommitFailed> context) =>
        UpdateOrderWithRetryAsync(context.Message.OrderId, order =>
        {
            if (order.Status is not (OrderStatus.Cancelled or OrderStatus.Refunding))
            {
                order.MarkRefunding(context.Message.Reason);
            }
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<ShipmentFailed> context) =>
        UpdateOrderWithRetryAsync(context.Message.OrderId, order =>
        {
            if (order.Status is not (OrderStatus.Cancelled or OrderStatus.Refunding))
            {
                order.MarkRefunding(context.Message.Reason);
            }
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<OrderConfirmed> context) =>
        UpdateOrderWithRetryAsync(context.Message.OrderId, order =>
        {
            if (order.Status is not (OrderStatus.Cancelled or OrderStatus.Refunding))
            {
                order.MarkConfirmed(context.Message.ShipmentId, context.Message.TrackingNumber);
            }
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<OrderCancelled> context) =>
        UpdateOrderWithRetryAsync(context.Message.OrderId, order =>
        {
            if (order.Status != OrderStatus.Confirmed)
            {
                order.MarkCancelled(context.Message.Reason);
            }
        }, context.CancellationToken);

    private async Task UpdateOrderWithRetryAsync(
        Guid orderId,
        Action<ShopSphere.Services.Order.Domain.Order> mutate,
        CancellationToken ct)
    {
        const int maxRetries = 3;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var order = await _dbContext.Orders
                    .FirstOrDefaultAsync(o => o.Id == orderId, ct);

                if (order is null)
                {
                    _logger.LogWarning("Order {OrderId} not found during state synchronization.", orderId);
                    return;
                }

                mutate(order);
                await _dbContext.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrency conflict updating Order {OrderId} on attempt {Attempt}", orderId, attempt);
                if (attempt == maxRetries)
                {
                    throw;
                }

                _dbContext.ClearChangeTracker();
                await Task.Delay(50 * attempt, ct);
            }
        }
    }
}

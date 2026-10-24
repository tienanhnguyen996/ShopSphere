using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Order.Application.Common.Interfaces;
using ShopSphere.Services.Order.Application.Contracts;
using ShopSphere.Services.Order.Application.Orders.Dtos;
using ShopSphere.Services.Order.Domain.Exceptions;

namespace ShopSphere.Services.Order.Application.Orders.Commands.CancelOrder;

public record CancelOrderCommand(
    Guid OrderId,
    string CustomerId,
    string Reason);

public interface ICancelOrderCommandHandler
{
    Task<Result<OrderDto>> HandleAsync(CancelOrderCommand command, CancellationToken ct = default);
}

public class CancelOrderCommandHandler : ICancelOrderCommandHandler
{
    private readonly IOrderDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<CancelOrderCommandHandler> _logger;

    public CancelOrderCommandHandler(
        IOrderDbContext dbContext,
        IPublishEndpoint publishEndpoint,
        ILogger<CancelOrderCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<OrderDto>> HandleAsync(CancelOrderCommand command, CancellationToken ct = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);

        if (order is null)
        {
            return Result.Failure<OrderDto>(
                Error.NotFound("Order.NotFound", $"Order {command.OrderId} was not found."));
        }

        if (!string.IsNullOrWhiteSpace(command.CustomerId) &&
            !string.Equals(order.CustomerId, command.CustomerId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<OrderDto>(
                Error.NotFound("Order.NotFound", $"Order {command.OrderId} was not found."));
        }

        var reason = string.IsNullOrWhiteSpace(command.Reason)
            ? "Cancelled by customer."
            : command.Reason.Trim();

        try
        {
            order.MarkCancelled(reason);
        }
        catch (InvalidOrderStateTransitionException ex)
        {
            return Result.Failure<OrderDto>(
                Error.Conflict("Order.InvalidStateTransition", ex.Message));
        }

        var cancelEvt = new OrderCancelled(order.Id, order.CustomerId, reason);
        var envelope = MessageEnvelope<OrderCancelled>.Create(
            cancelEvt,
            order.Id.ToString(),
            correlationId: order.Id);

        await _publishEndpoint.Publish(envelope, ct);
        await _publishEndpoint.Publish(cancelEvt, ct);

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Cancelled Order {OrderId} for Customer {CustomerId}. Reason: {Reason}",
            order.Id, order.CustomerId, reason);

        return Result.Success(order.ToDto());
    }
}

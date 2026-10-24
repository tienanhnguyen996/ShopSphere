using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Order.Application.Common.Interfaces;
using ShopSphere.Services.Order.Application.Orders.Dtos;

namespace ShopSphere.Services.Order.Application.Orders.Queries.GetOrderById;

public record GetOrderByIdQuery(Guid OrderId, string? CustomerId = null);

public interface IGetOrderByIdQueryHandler
{
    Task<Result<OrderDto>> HandleAsync(GetOrderByIdQuery query, CancellationToken ct = default);
}

public class GetOrderByIdQueryHandler : IGetOrderByIdQueryHandler
{
    private readonly IOrderDbContext _dbContext;

    public GetOrderByIdQueryHandler(IOrderDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<Result<OrderDto>> HandleAsync(GetOrderByIdQuery query, CancellationToken ct = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == query.OrderId, ct);

        if (order is null)
        {
            return Result.Failure<OrderDto>(
                Error.NotFound("Order.NotFound", $"Order {query.OrderId} was not found."));
        }

        if (!string.IsNullOrWhiteSpace(query.CustomerId) &&
            !string.Equals(order.CustomerId, query.CustomerId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<OrderDto>(
                Error.NotFound("Order.NotFound", $"Order {query.OrderId} was not found."));
        }

        return Result.Success(order.ToDto());
    }
}

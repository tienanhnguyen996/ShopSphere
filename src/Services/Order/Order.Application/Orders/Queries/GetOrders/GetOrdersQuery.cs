using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Order.Application.Common.Interfaces;
using ShopSphere.Services.Order.Application.Orders.Dtos;

namespace ShopSphere.Services.Order.Application.Orders.Queries.GetOrders;

public record GetOrdersQuery(string CustomerId, int Page = 1, int PageSize = 20);

public interface IGetOrdersQueryHandler
{
    Task<Result<PagedOrderResult>> HandleAsync(GetOrdersQuery query, CancellationToken ct = default);
}

public class GetOrdersQueryHandler : IGetOrdersQueryHandler
{
    private readonly IOrderDbContext _dbContext;

    public GetOrdersQueryHandler(IOrderDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<Result<PagedOrderResult>> HandleAsync(GetOrdersQuery query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query.CustomerId))
        {
            return Result.Failure<PagedOrderResult>(
                Error.Validation("Order.CustomerIdRequired", "Customer ID is required."));
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => query.PageSize
        };

        var normalizedCustomer = query.CustomerId.Trim();
        var baseQuery = _dbContext.Orders
            .Include(o => o.Items)
            .Where(o => o.CustomerId == normalizedCustomer);

        var totalCount = await baseQuery.CountAsync(ct);

        var orders = await baseQuery
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = orders.Select(o => o.ToDto()).ToList();

        return Result.Success(new PagedOrderResult(dtos, totalCount, page, pageSize));
    }
}

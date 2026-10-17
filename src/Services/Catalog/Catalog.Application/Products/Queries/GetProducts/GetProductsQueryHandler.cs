using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Catalog.Application.Common.Interfaces;
using ShopSphere.Services.Catalog.Application.Common.Models;
using ShopSphere.Services.Catalog.Application.Products.Dtos;
using ShopSphere.Services.Catalog.Domain;

namespace ShopSphere.Services.Catalog.Application.Products.Queries.GetProducts;

public class GetProductsQueryHandler : IGetProductsQueryHandler
{
    private readonly ICatalogDbContext _dbContext;
    private readonly IValidator<GetProductsQuery> _validator;

    public GetProductsQueryHandler(
        ICatalogDbContext dbContext,
        IValidator<GetProductsQuery> validator)
    {
        _dbContext = dbContext;
        _validator = validator;
    }

    public async Task<Result<PagedResult<ProductDto>>> HandleAsync(
        GetProductsQuery query,
        CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(query, ct);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
            return Result.Failure<PagedResult<ProductDto>>(Error.Validation("Query.ValidationFailed", errors));
        }

        var baseQuery = _dbContext.Products.AsNoTracking();

        if (query.OnlyActive)
        {
            baseQuery = baseQuery.Where(p => p.Status == ProductStatus.Active);
        }

        var totalCount = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderBy(p => p.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Description,
                p.Sku.Value,
                p.Price.Amount,
                p.Price.Currency,
                p.Status.ToString(),
                p.PriceVersion,
                p.CreatedAtUtc,
                p.UpdatedAtUtc))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<ProductDto>(items, totalCount, query.Page, query.PageSize));
    }
}

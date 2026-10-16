using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Catalog.Application.Common.Interfaces;
using ShopSphere.Services.Catalog.Application.Products.Dtos;
using ShopSphere.Services.Catalog.Domain;

namespace ShopSphere.Services.Catalog.Application.Products.Queries.GetProductBySku;

public class GetProductBySkuQueryHandler : IGetProductBySkuQueryHandler
{
    private readonly ICatalogDbContext _dbContext;
    private readonly IValidator<GetProductBySkuQuery> _validator;

    public GetProductBySkuQueryHandler(
        ICatalogDbContext dbContext,
        IValidator<GetProductBySkuQuery> validator)
    {
        _dbContext = dbContext;
        _validator = validator;
    }

    public virtual async Task<Result<ProductDto>> HandleAsync(
        GetProductBySkuQuery query,
        CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(query, ct);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
            return Result.Failure<ProductDto>(Error.Validation("Query.ValidationFailed", errors));
        }

        var sku = new Sku(query.Sku);
        var product = await _dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Sku == sku, ct);

        if (product is null)
        {
            return Result.Failure<ProductDto>(Error.NotFound(
                "Product.NotFound",
                $"Product with SKU '{query.Sku}' was not found."));
        }

        var dto = new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.Sku.Value,
            product.Price.Amount,
            product.Price.Currency,
            product.Status.ToString(),
            product.PriceVersion,
            product.CreatedAtUtc,
            product.UpdatedAtUtc);

        return Result.Success(dto);
    }
}

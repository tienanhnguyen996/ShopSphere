using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Catalog.Application.Common.Interfaces;
using ShopSphere.Services.Catalog.Application.Products.Dtos;
using ShopSphere.Services.Catalog.Domain;

namespace ShopSphere.Services.Catalog.Application.Products.Commands.CreateProduct;

public sealed class CreateProductCommandHandler
{
    private readonly ICatalogDbContext _dbContext;
    private readonly IValidator<CreateProductCommand> _validator;

    public CreateProductCommandHandler(
        ICatalogDbContext dbContext,
        IValidator<CreateProductCommand> validator)
    {
        _dbContext = dbContext;
        _validator = validator;
    }

    public async Task<Result<ProductDto>> HandleAsync(
        CreateProductCommand command,
        CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
            return Result.Failure<ProductDto>(Error.Validation("Product.ValidationFailed", errors));
        }

        var sku = new Sku(command.Sku);
        var skuExists = await _dbContext.Products
            .AnyAsync(p => p.Sku == sku, ct);

        if (skuExists)
        {
            return Result.Failure<ProductDto>(Error.Conflict(
                "Product.DuplicateSku",
                $"Product with SKU '{command.Sku}' already exists."));
        }

        var product = Product.Create(
            Guid.NewGuid(),
            command.Name,
            command.Description,
            sku,
            new Money(command.PriceAmount, command.Currency));

        if (command.ActivateImmediately)
        {
            product.Activate();
        }

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(ct);

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

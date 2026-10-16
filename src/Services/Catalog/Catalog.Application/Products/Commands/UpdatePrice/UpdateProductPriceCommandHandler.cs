using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.BuildingBlocks.Messaging.Serialization;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.Services.Catalog.Application.Common.Interfaces;
using ShopSphere.Services.Catalog.Application.Products.Dtos;
using ShopSphere.Services.Catalog.Domain;
using ShopSphere.Services.Catalog.Domain.Events;

namespace ShopSphere.Services.Catalog.Application.Products.Commands.UpdatePrice;

public sealed class UpdateProductPriceCommandHandler
{
    private readonly ICatalogDbContext _dbContext;
    private readonly IValidator<UpdateProductPriceCommand> _validator;

    public UpdateProductPriceCommandHandler(
        ICatalogDbContext dbContext,
        IValidator<UpdateProductPriceCommand> validator)
    {
        _dbContext = dbContext;
        _validator = validator;
    }

    public async Task<Result<ProductDto>> HandleAsync(
        UpdateProductPriceCommand command,
        CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
            return Result.Failure<ProductDto>(Error.Validation("Product.ValidationFailed", errors));
        }

        var sku = new Sku(command.Sku);
        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Sku == sku, ct);

        if (product is null)
        {
            return Result.Failure<ProductDto>(Error.NotFound(
                "Product.NotFound",
                $"Product with SKU '{command.Sku}' was not found."));
        }

        var currency = command.Currency ?? product.Price.Currency;
        var oldPrice = product.Price;
        var newPrice = new Money(command.NewPrice, currency);

        product.UpdatePrice(newPrice);

        // Record price history
        var history = new PriceHistory(
            Guid.NewGuid(),
            product.Id,
            product.Sku.Value,
            oldPrice.Amount,
            newPrice.Amount,
            currency,
            product.PriceVersion,
            DateTime.UtcNow);
        _dbContext.PriceHistories.Add(history);

        // Publish outbox event in the same transaction
        var domainEvent = product.DomainEvents
            .OfType<ProductPriceChangedDomainEvent>()
            .LastOrDefault();

        if (domainEvent is not null)
        {
            var outboxMessage = new OutboxMessage
            {
                Id = domainEvent.EventId,
                Type = typeof(ProductPriceChangedDomainEvent).FullName ?? nameof(ProductPriceChangedDomainEvent),
                Content = ShopSphereJsonDefaults.Serialize(domainEvent),
                OccurredAtUtc = domainEvent.OccurredAtUtc
            };
            _dbContext.OutboxMessages.Add(outboxMessage);
        }

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

using FluentValidation;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Cart.Application.Carts.Dtos;
using ShopSphere.Services.Cart.Application.Common.Interfaces;

namespace ShopSphere.Services.Cart.Application.Carts.Queries.GetCheckoutQuote;

public record GetCheckoutQuoteQuery(string CustomerId);

public class GetCheckoutQuoteQueryValidator : AbstractValidator<GetCheckoutQuoteQuery>
{
    public GetCheckoutQuoteQueryValidator()
    {
        RuleFor(x => x.CustomerId)
            .NotEmpty().WithMessage("Customer ID is required.");
    }
}

public interface IGetCheckoutQuoteQueryHandler
{
    Task<Result<CheckoutQuoteDto>> HandleAsync(GetCheckoutQuoteQuery query, CancellationToken ct = default);
}

public class GetCheckoutQuoteQueryHandler : IGetCheckoutQuoteQueryHandler
{
    private readonly ICartRepository _cartRepository;
    private readonly ICatalogClient _catalogClient;
    private readonly IValidator<GetCheckoutQuoteQuery> _validator;

    public GetCheckoutQuoteQueryHandler(
        ICartRepository cartRepository,
        ICatalogClient catalogClient,
        IValidator<GetCheckoutQuoteQuery> validator)
    {
        _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));
        _catalogClient = catalogClient ?? throw new ArgumentNullException(nameof(catalogClient));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<Result<CheckoutQuoteDto>> HandleAsync(GetCheckoutQuoteQuery query, CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(query, ct);
        if (!validationResult.IsValid)
        {
            var firstError = validationResult.Errors[0];
            return Result.Failure<CheckoutQuoteDto>(Error.Validation("Cart.Validation", firstError.ErrorMessage));
        }

        var cart = await _cartRepository.GetCartAsync(query.CustomerId, ct);
        if (cart is null || cart.Items.Count == 0)
        {
            return Result.Success(new CheckoutQuoteDto(
                query.CustomerId,
                Array.Empty<CheckoutQuoteItemDto>(),
                0m,
                "USD",
                new Dictionary<string, int>(),
                true,
                Array.Empty<string>()));
        }

        var quoteItems = new List<CheckoutQuoteItemDto>();
        var priceVersions = new Dictionary<string, int>();
        var validationErrors = new List<string>();
        var currency = "USD";
        var isValid = true;

        foreach (var item in cart.Items)
        {
            var catalogResult = await _catalogClient.GetProductBySkuAsync(item.Sku, ct);

            if (catalogResult.IsFailure)
            {
                if (catalogResult.Error.Type == ErrorType.NotFound)
                {
                    isValid = false;
                    validationErrors.Add($"Product '{item.Sku}' was not found in the catalog.");
                    quoteItems.Add(new CheckoutQuoteItemDto(
                        item.Sku,
                        item.Sku,
                        item.Quantity,
                        0m,
                        0m,
                        currency,
                        0,
                        false));
                    continue;
                }

                // If catalog is unavailable or timing out, return failure with 503 guidance
                return Result.Failure<CheckoutQuoteDto>(catalogResult.Error);
            }

            var product = catalogResult.Value;
            currency = product.Currency;

            var isActive = string.Equals(product.Status, "Active", StringComparison.OrdinalIgnoreCase);
            if (!isActive)
            {
                isValid = false;
                validationErrors.Add($"Product '{item.Sku}' is currently '{product.Status}' and cannot be purchased.");
            }

            var lineTotal = isActive ? item.Quantity * product.Price : 0m;
            priceVersions[item.Sku] = product.PriceVersion;

            quoteItems.Add(new CheckoutQuoteItemDto(
                item.Sku,
                product.Name,
                item.Quantity,
                product.Price,
                lineTotal,
                product.Currency,
                product.PriceVersion,
                isActive));
        }

        var subtotal = quoteItems.Where(i => i.IsActive).Sum(i => i.LineTotal);

        var quote = new CheckoutQuoteDto(
            query.CustomerId,
            quoteItems.AsReadOnly(),
            subtotal,
            currency,
            priceVersions,
            isValid,
            validationErrors.AsReadOnly());

        return Result.Success(quote);
    }
}

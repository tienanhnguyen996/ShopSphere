using FluentValidation;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Cart.Application.Carts.Dtos;
using ShopSphere.Services.Cart.Application.Common.Interfaces;

namespace ShopSphere.Services.Cart.Application.Carts.Queries.GetCart;

public record GetCartQuery(string CustomerId);

public class GetCartQueryValidator : AbstractValidator<GetCartQuery>
{
    public GetCartQueryValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("Customer ID is required.");
    }
}

public interface IGetCartQueryHandler
{
    Task<Result<CartDto>> HandleAsync(GetCartQuery query, CancellationToken ct = default);
}

public class GetCartQueryHandler : IGetCartQueryHandler
{
    private readonly ICartRepository _cartRepository;
    private readonly IValidator<GetCartQuery> _validator;

    public GetCartQueryHandler(ICartRepository cartRepository, IValidator<GetCartQuery> validator)
    {
        _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<Result<CartDto>> HandleAsync(GetCartQuery query, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(query, ct);
        if (!validation.IsValid)
        {
            return Result.Failure<CartDto>(Error.Validation("Cart.Validation", validation.Errors[0].ErrorMessage));
        }

        var cart = await _cartRepository.GetCartAsync(query.CustomerId, ct);
        var items = cart?.Items.Select(i => new CartItemDto(i.Sku, i.Quantity)).ToList()
            ?? new List<CartItemDto>();

        var dto = new CartDto(
            query.CustomerId,
            items.AsReadOnly(),
            cart?.TotalQuantity ?? 0,
            cart?.CreatedAtUtc ?? DateTime.UtcNow,
            cart?.LastModifiedUtc ?? DateTime.UtcNow);

        return Result.Success(dto);
    }
}

using FluentValidation;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Cart.Application.Carts.Dtos;
using ShopSphere.Services.Cart.Application.Common.Interfaces;

namespace ShopSphere.Services.Cart.Application.Carts.Commands.RemoveItem;

public record RemoveCartItemCommand(string CustomerId, string Sku);

public class RemoveCartItemCommandValidator : AbstractValidator<RemoveCartItemCommand>
{
    public RemoveCartItemCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("Customer ID is required.");
        RuleFor(x => x.Sku).NotEmpty().WithMessage("SKU is required.");
    }
}

public interface IRemoveCartItemCommandHandler
{
    Task<Result<CartDto>> HandleAsync(RemoveCartItemCommand command, CancellationToken ct = default);
}

public class RemoveCartItemCommandHandler : IRemoveCartItemCommandHandler
{
    private readonly ICartRepository _cartRepository;
    private readonly IValidator<RemoveCartItemCommand> _validator;

    public RemoveCartItemCommandHandler(ICartRepository cartRepository, IValidator<RemoveCartItemCommand> validator)
    {
        _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<Result<CartDto>> HandleAsync(RemoveCartItemCommand command, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            return Result.Failure<CartDto>(Error.Validation("Cart.Validation", validation.Errors[0].ErrorMessage));
        }

        var cart = await _cartRepository.GetCartAsync(command.CustomerId, ct);
        if (cart is not null)
        {
            cart.RemoveItem(command.Sku);
            await _cartRepository.SaveCartAsync(cart, ct);
        }

        var items = cart?.Items.Select(i => new CartItemDto(i.Sku, i.Quantity)).ToList() ?? new List<CartItemDto>();
        var dto = new CartDto(command.CustomerId, items.AsReadOnly(), cart?.TotalQuantity ?? 0, cart?.CreatedAtUtc ?? DateTime.UtcNow, cart?.LastModifiedUtc ?? DateTime.UtcNow);
        return Result.Success(dto);
    }
}

using FluentValidation;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Cart.Application.Carts.Dtos;
using ShopSphere.Services.Cart.Application.Common.Interfaces;
using ShopSphere.Services.Cart.Domain.Exceptions;
using CartAggregate = ShopSphere.Services.Cart.Domain.Cart;

namespace ShopSphere.Services.Cart.Application.Carts.Commands.UpdateQuantity;

public record UpdateCartItemQuantityCommand(string CustomerId, string Sku, int Quantity);

public class UpdateCartItemQuantityCommandValidator : AbstractValidator<UpdateCartItemQuantityCommand>
{
    public UpdateCartItemQuantityCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("Customer ID is required.");
        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("SKU is required.")
            .Matches("^[A-Z0-9-_]+$").WithMessage("SKU must be uppercase alphanumeric.");
        RuleFor(x => x.Quantity)
            .InclusiveBetween(0, 99).WithMessage("Quantity must be between 0 and 99.");
    }
}

public interface IUpdateCartItemQuantityCommandHandler
{
    Task<Result<CartDto>> HandleAsync(UpdateCartItemQuantityCommand command, CancellationToken ct = default);
}

public class UpdateCartItemQuantityCommandHandler : IUpdateCartItemQuantityCommandHandler
{
    private readonly ICartRepository _cartRepository;
    private readonly IValidator<UpdateCartItemQuantityCommand> _validator;

    public UpdateCartItemQuantityCommandHandler(ICartRepository cartRepository, IValidator<UpdateCartItemQuantityCommand> validator)
    {
        _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<Result<CartDto>> HandleAsync(UpdateCartItemQuantityCommand command, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            return Result.Failure<CartDto>(Error.Validation("Cart.Validation", validation.Errors[0].ErrorMessage));
        }

        var cart = await _cartRepository.GetCartAsync(command.CustomerId, ct)
            ?? new CartAggregate(command.CustomerId);

        try
        {
            cart.UpdateItemQuantity(command.Sku, command.Quantity);
        }
        catch (DomainValidationException ex)
        {
            return Result.Failure<CartDto>(Error.Validation(ex.Code, ex.Message));
        }

        await _cartRepository.SaveCartAsync(cart, ct);

        var items = cart.Items.Select(i => new CartItemDto(i.Sku, i.Quantity)).ToList();
        var dto = new CartDto(cart.CustomerId, items.AsReadOnly(), cart.TotalQuantity, cart.CreatedAtUtc, cart.LastModifiedUtc);
        return Result.Success(dto);
    }
}

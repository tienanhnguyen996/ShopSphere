using FluentValidation;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Cart.Application.Common.Interfaces;

namespace ShopSphere.Services.Cart.Application.Carts.Commands.ClearCart;

public record ClearCartCommand(string CustomerId);

public class ClearCartCommandValidator : AbstractValidator<ClearCartCommand>
{
    public ClearCartCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("Customer ID is required.");
    }
}

public interface IClearCartCommandHandler
{
    Task<Result<bool>> HandleAsync(ClearCartCommand command, CancellationToken ct = default);
}

public class ClearCartCommandHandler : IClearCartCommandHandler
{
    private readonly ICartRepository _cartRepository;
    private readonly IValidator<ClearCartCommand> _validator;

    public ClearCartCommandHandler(ICartRepository cartRepository, IValidator<ClearCartCommand> validator)
    {
        _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<Result<bool>> HandleAsync(ClearCartCommand command, CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            return Result.Failure<bool>(Error.Validation("Cart.Validation", validation.Errors[0].ErrorMessage));
        }

        var cleared = await _cartRepository.ClearCartAsync(command.CustomerId, ct);
        return Result.Success(cleared);
    }
}

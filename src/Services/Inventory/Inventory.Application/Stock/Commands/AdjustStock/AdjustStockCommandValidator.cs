using FluentValidation;

namespace ShopSphere.Services.Inventory.Application.Stock.Commands.AdjustStock;

public class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("SKU cannot be empty.")
            .Matches(@"^[A-Z0-9-_]+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .WithMessage("SKU must contain only alphanumeric characters, dashes, or underscores.");

        RuleFor(x => x.Quantity)
            .NotEqual(0).WithMessage("Adjustment quantity cannot be zero.");

        RuleFor(x => x.Reason)
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters.");
    }
}

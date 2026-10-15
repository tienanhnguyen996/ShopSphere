using FluentValidation;

namespace ShopSphere.Services.Catalog.Application.Products.Commands.UpdatePrice;

public sealed class UpdateProductPriceCommandValidator : AbstractValidator<UpdateProductPriceCommand>
{
    public UpdateProductPriceCommandValidator()
    {
        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("SKU cannot be empty.")
            .Length(3, 50).WithMessage("SKU length must be between 3 and 50 characters.");

        RuleFor(x => x.NewPrice)
            .GreaterThan(0).WithMessage("Product price must be greater than zero.");

        RuleFor(x => x.Currency)
            .Length(3).WithMessage("Currency must be a valid 3-letter ISO code.")
            .When(x => !string.IsNullOrEmpty(x.Currency));
    }
}

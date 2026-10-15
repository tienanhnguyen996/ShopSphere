using FluentValidation;

namespace ShopSphere.Services.Catalog.Application.Products.Commands.CreateProduct;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name cannot be empty.")
            .MaximumLength(200).WithMessage("Product name cannot exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Product description cannot exceed 2000 characters.");

        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("SKU cannot be null or whitespace.")
            .Length(3, 50).WithMessage("SKU length must be between 3 and 50 characters.")
            .Matches(@"^[A-Z0-9-_]+$").WithMessage("SKU must be alphanumeric (letters, numbers, hyphens, and underscores).");

        RuleFor(x => x.PriceAmount)
            .GreaterThan(0).WithMessage("Product price must be greater than zero.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency cannot be empty.")
            .Length(3).WithMessage("Currency must be a valid 3-letter ISO code.");
    }
}

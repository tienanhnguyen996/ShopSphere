using FluentValidation;

namespace ShopSphere.Services.Catalog.Application.Products.Queries.GetProductBySku;

public sealed class GetProductBySkuQueryValidator : AbstractValidator<GetProductBySkuQuery>
{
    public GetProductBySkuQueryValidator()
    {
        RuleFor(x => x.Sku)
            .NotEmpty().WithMessage("SKU cannot be empty.");
    }
}

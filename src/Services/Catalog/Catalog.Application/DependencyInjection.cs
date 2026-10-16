using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.Services.Catalog.Application.Products.Commands.CreateProduct;
using ShopSphere.Services.Catalog.Application.Products.Commands.UpdatePrice;
using ShopSphere.Services.Catalog.Application.Products.Queries.GetProductBySku;
using ShopSphere.Services.Catalog.Application.Products.Queries.GetProducts;

namespace ShopSphere.Services.Catalog.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateProductCommandValidator>();

        services.AddScoped<CreateProductCommandHandler>();
        services.AddScoped<UpdateProductPriceCommandHandler>();

        services.AddScoped<GetProductBySkuQueryHandler>();
        services.AddScoped<IGetProductBySkuQueryHandler>(sp => sp.GetRequiredService<GetProductBySkuQueryHandler>());

        services.AddScoped<GetProductsQueryHandler>();
        services.AddScoped<IGetProductsQueryHandler>(sp => sp.GetRequiredService<GetProductsQueryHandler>());

        return services;
    }
}

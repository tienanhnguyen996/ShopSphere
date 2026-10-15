using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.Services.Inventory.Application.Stock.Commands.AdjustStock;
using ShopSphere.Services.Inventory.Application.Stock.Queries.GetStockBySku;

namespace ShopSphere.Services.Inventory.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IAdjustStockCommandHandler, AdjustStockCommandHandler>();
        services.AddScoped<IGetStockBySkuQueryHandler, GetStockBySkuQueryHandler>();

        return services;
    }
}

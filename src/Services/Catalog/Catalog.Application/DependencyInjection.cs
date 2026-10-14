using Microsoft.Extensions.DependencyInjection;

namespace ShopSphere.Services.Catalog.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register application services, handlers, validators
        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;

namespace ShopSphere.Services.ServiceName.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Add MediatR, FluentValidation, etc.
        return services;
    }
}

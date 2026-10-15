using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ShopSphere.Services.ServiceName.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ServiceNameDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("Database"),
                b => b.MigrationsAssembly(typeof(DependencyInjection).Assembly.FullName)));

        // Add repositories, Outbox, MassTransit

        return services;
    }
}

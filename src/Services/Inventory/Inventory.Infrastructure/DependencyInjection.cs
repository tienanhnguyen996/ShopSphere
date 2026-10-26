using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.BuildingBlocks.Persistence.Interceptors;
using ShopSphere.Services.Inventory.Application.Common.Interfaces;

namespace ShopSphere.Services.Inventory.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<AuditSaveChangesInterceptor>();

        var connectionString = configuration.GetConnectionString("InventoryDatabase")
            ?? "Server=localhost,1433;Database=ShopSphereInventory;User Id=sa;Password=DevPassword123!;TrustServerCertificate=True;";

        services.AddDbContext<InventoryDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditSaveChangesInterceptor>();
            options.UseSqlServer(connectionString)
                   .AddInterceptors(interceptor);
        });

        services.AddScoped<IInventoryDbContext>(sp => sp.GetRequiredService<InventoryDbContext>());

        return services;
    }
}

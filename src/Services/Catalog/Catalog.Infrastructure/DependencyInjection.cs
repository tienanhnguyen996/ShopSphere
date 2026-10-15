using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.BuildingBlocks.Persistence.Interceptors;

namespace ShopSphere.Services.Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<AuditSaveChangesInterceptor>();

        var connectionString = configuration.GetConnectionString("CatalogDatabase")
            ?? "Server=localhost,1433;Database=ShopSphereCatalog;User Id=sa;Password=Your_password123;TrustServerCertificate=True;";

        services.AddDbContext<CatalogDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditSaveChangesInterceptor>();
            options.UseSqlServer(connectionString)
                   .AddInterceptors(interceptor);
        });

        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.BuildingBlocks.Persistence.Interceptors;
using ShopSphere.Services.Catalog.Application.Common.Interfaces;
using ShopSphere.Services.Catalog.Infrastructure.Caching;

namespace ShopSphere.Services.Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<AuditSaveChangesInterceptor>();

        var connectionString = configuration.GetConnectionString("Database")
            ?? configuration.GetConnectionString("CatalogDatabase")
            ?? "Server=localhost,1433;Database=ShopSphereCatalog;User Id=sa;Password=DevPassword123!;TrustServerCertificate=True;";

        services.AddDbContext<CatalogDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditSaveChangesInterceptor>();
            options.UseSqlServer(connectionString)
                   .AddInterceptors(interceptor)
                   .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<ICatalogDbContext>(sp => sp.GetRequiredService<CatalogDbContext>());

        var redisConnection = configuration.GetConnectionString("Redis")
            ?? "localhost:6379";

        services.AddStackExchangeRedisCache(options =>
        {
            var configOptions = StackExchange.Redis.ConfigurationOptions.Parse(redisConnection);
            configOptions.CertificateValidation += (sender, cert, chain, errors) => true;
            options.ConfigurationOptions = configOptions;
            options.InstanceName = "ShopSphereCatalog:";
        });

        services.AddSingleton<ICacheService, DistributedCacheService>();

        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.Services.Shipping.Application.Abstractions;
using ShopSphere.Services.Shipping.Application.Commands;
using ShopSphere.Services.Shipping.Application.Queries;
using ShopSphere.Services.Shipping.Infrastructure.Carriers;
using ShopSphere.Services.Shipping.Infrastructure.Persistence;

namespace ShopSphere.Services.Shipping.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddShippingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ShippingDb")
            ?? "Server=localhost,1433;Database=ShopSphere_Shipping;User Id=sa;Password=ShopSphere_DevPassword123!;TrustServerCertificate=True;Encrypt=False";

        services.AddDbContext<ShippingDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsHistoryTable("__EFMigrationsHistory", ShippingDbContext.DefaultSchema);
                sql.EnableRetryOnFailure(3);
            });
        });

        services.AddScoped<IShippingDbContext>(sp => sp.GetRequiredService<ShippingDbContext>());
        services.AddSingleton<ICarrierProviderClient, MockCarrierProviderClient>();

        services.AddScoped<RecordCarrierHandoverCommandHandler>();
        services.AddScoped<RecordDeliveryCommandHandler>();
        services.AddScoped<CancelShipmentCommandHandler>();
        services.AddScoped<GetShipmentQueriesHandler>();

        return services;
    }
}

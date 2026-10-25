using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.BuildingBlocks.Resilience;
using ShopSphere.Services.Shipping.Application.Abstractions;
using ShopSphere.Services.Shipping.Application.Commands;
using ShopSphere.Services.Shipping.Application.Queries;
using ShopSphere.Services.Shipping.Infrastructure.Carriers;
using ShopSphere.Services.Shipping.Infrastructure.Configuration;
using ShopSphere.Services.Shipping.Infrastructure.Persistence;

namespace ShopSphere.Services.Shipping.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddShippingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ShippingDb")
            ?? "Server=localhost,1433;Database=ShopSphere_Shipping;User Id=sa;Password=DevPassword123!;TrustServerCertificate=True;Encrypt=False";

        services.AddDbContext<ShippingDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsHistoryTable("__EFMigrationsHistory", ShippingDbContext.DefaultSchema);
                sql.EnableRetryOnFailure(3);
            })
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IShippingDbContext>(sp => sp.GetRequiredService<ShippingDbContext>());

        services.Configure<CarrierSettings>(configuration.GetSection(CarrierSettings.SectionName));

        services.AddSingleton<MockCarrierProviderClient>();
        services.AddResilientHttpClient<ShippoCarrierClient, ShippoCarrierClient>();
        services.AddResilientHttpClient<GhnCarrierClient, GhnCarrierClient>();

        services.AddScoped<ICarrierProviderClient, RoutingCarrierProviderClient>();

        services.AddScoped<RecordCarrierHandoverCommandHandler>();
        services.AddScoped<RecordDeliveryCommandHandler>();
        services.AddScoped<CancelShipmentCommandHandler>();
        services.AddScoped<GetShipmentQueriesHandler>();

        return services;
    }
}

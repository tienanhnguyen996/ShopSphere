using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.BuildingBlocks.Idempotency;
using ShopSphere.BuildingBlocks.Persistence.Interceptors;
using ShopSphere.BuildingBlocks.Resilience;
using ShopSphere.Services.Order.Application.Common.Interfaces;
using ShopSphere.Services.Order.Infrastructure.Clients;
using ShopSphere.Services.Order.Infrastructure.Idempotency;

namespace ShopSphere.Services.Order.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<AuditSaveChangesInterceptor>();

        var connectionString = configuration.GetConnectionString("OrderDatabase")
            ?? "Server=localhost,1433;Database=ShopSphereOrders;User Id=sa;Password=DevPassword123!;TrustServerCertificate=True;";

        services.AddDbContext<OrderDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditSaveChangesInterceptor>();
            options.UseSqlServer(connectionString)
                   .AddInterceptors(interceptor)
                   .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IOrderDbContext>(sp => sp.GetRequiredService<OrderDbContext>());
        services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();

        var cartBaseUrl = configuration["Services:Cart:BaseUrl"] ?? "http://localhost:5002";
        services.AddResilientHttpClient<ICartClient, CartHttpClient>(client =>
        {
            client.BaseAddress = new Uri(cartBaseUrl);
        });

        return services;
    }
}

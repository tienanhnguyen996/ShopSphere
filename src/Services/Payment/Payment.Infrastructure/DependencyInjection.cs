using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.BuildingBlocks.Persistence.Interceptors;
using ShopSphere.BuildingBlocks.Resilience;
using ShopSphere.Services.Payment.Application.Common.Interfaces;
using ShopSphere.Services.Payment.Infrastructure.Clients;
using ShopSphere.Services.Payment.Infrastructure.Configuration;

namespace ShopSphere.Services.Payment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<AuditSaveChangesInterceptor>();

        var connectionString = configuration.GetConnectionString("Database")
            ?? configuration.GetConnectionString("PaymentDatabase")
            ?? "Server=localhost,1433;Database=ShopSpherePayment;User Id=sa;Password=DevPassword123!;TrustServerCertificate=True;";

        services.AddDbContext<PaymentDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditSaveChangesInterceptor>();
            options.UseSqlServer(connectionString)
                   .AddInterceptors(interceptor)
                   .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IPaymentDbContext>(sp => sp.GetRequiredService<PaymentDbContext>());

        services.Configure<PaymentSettings>(configuration.GetSection(PaymentSettings.SectionName));

        services.AddResilientHttpClient<MockPaymentProviderClient, MockPaymentProviderClient>();
        services.AddResilientHttpClient<StripePaymentProviderClient, StripePaymentProviderClient>();
        services.AddResilientHttpClient<MoMoPaymentProviderClient, MoMoPaymentProviderClient>();
        services.AddResilientHttpClient<VnPayPaymentProviderClient, VnPayPaymentProviderClient>();

        services.AddScoped<IPaymentProviderClient, RoutingPaymentProviderClient>();

        return services;
    }
}

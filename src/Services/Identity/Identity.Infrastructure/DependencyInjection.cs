using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.BuildingBlocks.Persistence.Interceptors;
using ShopSphere.Services.Identity.Application.Common.Interfaces;
using ShopSphere.Services.Identity.Infrastructure.Services;

namespace ShopSphere.Services.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<AuditSaveChangesInterceptor>();

        var connectionString = configuration.GetConnectionString("IdentityDatabase")
            ?? "Server=localhost,1433;Database=ShopSphereIdentity;User Id=sa;Password=DevPassword123!;TrustServerCertificate=True;";

        services.AddDbContext<IdentityDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditSaveChangesInterceptor>();
            options.UseSqlServer(connectionString)
                   .AddInterceptors(interceptor);
        });

        services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());

        services.AddSingleton<IPasswordHashService, PasswordHashService>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }

    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddInfrastructure(configuration);
    }
}

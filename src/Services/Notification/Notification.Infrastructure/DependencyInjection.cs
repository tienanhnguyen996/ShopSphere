using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.Services.Notification.Application.Abstractions;
using ShopSphere.Services.Notification.Application.Commands;
using ShopSphere.Services.Notification.Application.Queries;
using ShopSphere.Services.Notification.Infrastructure.Persistence;
using ShopSphere.Services.Notification.Infrastructure.Senders;

namespace ShopSphere.Services.Notification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("NotificationDb")
            ?? configuration.GetConnectionString("Database")
            ?? "Server=localhost,1433;Database=ShopSphere_Notification;User Id=sa;Password=DevPassword123!;TrustServerCertificate=True;Encrypt=False";

        services.AddDbContext<NotificationDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsHistoryTable("__EFMigrationsHistory", NotificationDbContext.DefaultSchema);
                sql.EnableRetryOnFailure(3);
            })
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<INotificationDbContext>(sp => sp.GetRequiredService<NotificationDbContext>());
        services.AddSingleton<INotificationSender, MockNotificationSender>();

        services.AddScoped<RetryNotificationCommandHandler>();
        services.AddScoped<GetNotificationQueriesHandler>();

        return services;
    }
}

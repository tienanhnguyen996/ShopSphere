using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShopSphere.BuildingBlocks.Messaging.Configuration;
using ShopSphere.BuildingBlocks.Persistence.Extensions;
using ShopSphere.Services.Notification.Application.Consumers;
using ShopSphere.Services.Notification.Infrastructure;
using ShopSphere.Services.Notification.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddNotificationInfrastructure(builder.Configuration);

builder.Services.AddShopSphereMessaging(builder.Configuration, busConfig =>
{
    busConfig.AddConsumer<OrderNotificationConsumer>();
    busConfig.AddShopSphereOutbox<NotificationDbContext>();
});

var host = builder.Build();

try
{
    using var scope = host.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetService<NotificationDbContext>();
    if (dbContext is not null)
    {
        await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(dbContext.Database);
    }
}
catch
{
    // Ignore concurrent migration race if API is migrating simultaneously
}

host.Run();

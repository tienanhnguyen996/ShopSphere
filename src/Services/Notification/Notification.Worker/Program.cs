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
host.Run();

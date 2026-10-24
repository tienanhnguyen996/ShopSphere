using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShopSphere.BuildingBlocks.Messaging.Configuration;
using ShopSphere.BuildingBlocks.Persistence.Extensions;
using ShopSphere.Services.Inventory.Application.Consumers;
using ShopSphere.Services.Inventory.Application.Workers;
using ShopSphere.Services.Inventory.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddShopSphereMessaging(builder.Configuration, busConfig =>
{
    busConfig.AddConsumer<ReserveInventoryConsumer>();
    busConfig.AddConsumer<ReleaseInventoryConsumer>();
    busConfig.AddConsumer<CommitInventoryConsumer>();
    busConfig.AddConsumer<ShipmentHandedOverConsumer>();
    busConfig.AddShopSphereOutbox<InventoryDbContext>();
});

builder.Services.AddHostedService<ReservationExpiryWorker>();

var host = builder.Build();
host.Run();

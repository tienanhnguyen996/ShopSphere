using Microsoft.Extensions.Hosting;
using ShopSphere.BuildingBlocks.Messaging.Configuration;
using ShopSphere.BuildingBlocks.Persistence.Extensions;
using ShopSphere.Services.Shipping.Application.Consumers;
using ShopSphere.Services.Shipping.Infrastructure;
using ShopSphere.Services.Shipping.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddShippingInfrastructure(builder.Configuration);

builder.Services.AddShopSphereMessaging(builder.Configuration, busConfig =>
{
    busConfig.AddConsumer<CreateShipmentConsumer>();
    busConfig.AddShopSphereOutbox<ShippingDbContext>();
});

var host = builder.Build();
host.Run();

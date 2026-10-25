using MassTransit;
using Microsoft.Extensions.Hosting;
using ShopSphere.BuildingBlocks.Messaging.Configuration;
using ShopSphere.BuildingBlocks.Persistence.Extensions;
using ShopSphere.Services.Order.Application.Saga;
using ShopSphere.Services.Order.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOrderInfrastructure(builder.Configuration);

builder.Services.AddShopSphereMessaging(builder.Configuration, busConfig =>
{
    busConfig.AddSagaStateMachine<CheckoutStateMachine, CheckoutSagaState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;
            r.ExistingDbContext<OrderDbContext>();
            r.UseSqlServer();
        });

    busConfig.AddConsumer<OrderStateSyncConsumer>();
    busConfig.AddShopSphereOutbox<OrderDbContext>();
});

var host = builder.Build();
host.Run();

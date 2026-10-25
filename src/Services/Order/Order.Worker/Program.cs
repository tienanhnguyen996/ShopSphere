using MassTransit;
using Microsoft.Extensions.DependencyInjection;
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

try
{
    using var scope = host.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetService<OrderDbContext>();
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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShopSphere.BuildingBlocks.Messaging.Configuration;
using ShopSphere.BuildingBlocks.Persistence.Extensions;
using ShopSphere.Services.Payment.Application.Consumers;
using ShopSphere.Services.Payment.Application.Workers;
using ShopSphere.Services.Payment.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPaymentInfrastructure(builder.Configuration);

builder.Services.AddShopSphereMessaging(builder.Configuration, busConfig =>
{
    busConfig.AddConsumer<CollectPaymentConsumer>();
    busConfig.AddConsumer<RefundPaymentConsumer>();
    busConfig.AddShopSphereOutbox<PaymentDbContext>();
});

builder.Services.AddHostedService<PaymentReconciliationWorker>();

var host = builder.Build();

try
{
    using var scope = host.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetService<PaymentDbContext>();
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

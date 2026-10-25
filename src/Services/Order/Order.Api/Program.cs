using MassTransit;
using Microsoft.OpenApi;
using ShopSphere.BuildingBlocks.Messaging.Configuration;
using ShopSphere.BuildingBlocks.Observability;
using ShopSphere.BuildingBlocks.Persistence.Extensions;
using ShopSphere.BuildingBlocks.Security;
using ShopSphere.Services.Order.Api;
using ShopSphere.Services.Order.Api.Health;
using ShopSphere.Services.Order.Application;
using ShopSphere.Services.Order.Application.Saga;
using ShopSphere.Services.Order.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ShopSphere Order Service API",
        Version = "v1",
        Description = "Microservice managing customer orders, checkout price snapshots, idempotency, and the MassTransit Checkout State Machine Saga."
    });
});

builder.Services.AddShopSphereObservability(builder.Configuration, "OrderService");
builder.Services.AddShopSphereAuthentication(builder.Configuration);
builder.Services.AddShopSphereAuthorization();
builder.Services.AddOrderApplication();
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

builder.Services.AddHealthChecks()
    .AddCheck<SqlServerHealthCheck>("sqlserver", tags: ["db", "sql"])
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["messaging", "rabbitmq"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Order Service API v1");
    });

    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<OrderDbContext>();
        if (dbContext is not null)
        {
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(dbContext.Database);
        }
    }
    catch
    {
        // Ignore migration errors in isolated test environments without SQL Server
    }
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapOrderEndpoints();

app.Run();

public partial class Program { }

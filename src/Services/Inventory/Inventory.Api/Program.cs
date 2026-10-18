using Microsoft.OpenApi.Models;
using ShopSphere.BuildingBlocks.Messaging.Configuration;
using ShopSphere.BuildingBlocks.Observability;
using ShopSphere.BuildingBlocks.Persistence.Extensions;
using ShopSphere.Services.Inventory.Api;
using ShopSphere.Services.Inventory.Api.Health;
using ShopSphere.Services.Inventory.Application;
using ShopSphere.Services.Inventory.Application.Consumers;
using ShopSphere.Services.Inventory.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ShopSphere Inventory Service API",
        Version = "v1",
        Description = "Microservice managing stock allocations, reservation holds, commit workflows, and intake adjustments."
    });
});

builder.Services.AddShopSphereObservability(builder.Configuration, "InventoryService");
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddShopSphereMessaging(builder.Configuration, busConfig =>
{
    busConfig.AddConsumer<ReserveInventoryConsumer>();
    busConfig.AddConsumer<ReleaseInventoryConsumer>();
    busConfig.AddConsumer<CommitInventoryConsumer>();
    busConfig.AddShopSphereOutbox<InventoryDbContext>();
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Inventory Service API v1");
    });
}

app.UseHttpsRedirection();

app.MapInventoryEndpoints();

app.Run();

public partial class Program { }

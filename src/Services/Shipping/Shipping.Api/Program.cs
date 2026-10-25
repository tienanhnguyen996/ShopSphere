using Microsoft.OpenApi;
using ShopSphere.BuildingBlocks.Messaging.Configuration;
using ShopSphere.BuildingBlocks.Observability;
using ShopSphere.BuildingBlocks.Persistence.Extensions;
using ShopSphere.BuildingBlocks.Security;
using ShopSphere.Services.Shipping.Api.Endpoints;
using ShopSphere.Services.Shipping.Api.Health;
using ShopSphere.Services.Shipping.Application.Consumers;
using ShopSphere.Services.Shipping.Infrastructure;
using ShopSphere.Services.Shipping.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ShopSphere Shipping Service API",
        Version = "v1",
        Description = "Microservice managing shipment creation, carrier tracking labels, physical carrier handover, and delivery milestones."
    });
});

builder.Services.AddShopSphereObservability(builder.Configuration, "ShippingService");
builder.Services.AddShopSphereAuthentication(builder.Configuration);
builder.Services.AddShopSphereAuthorization();
builder.Services.AddShippingInfrastructure(builder.Configuration);

builder.Services.AddShopSphereMessaging(builder.Configuration, busConfig =>
{
    busConfig.AddConsumer<CreateShipmentConsumer>();
    busConfig.AddShopSphereOutbox<ShippingDbContext>();
});

builder.Services
    .AddHealthChecks()
    .AddCheck<SqlServerHealthCheck>("sqlserver", tags: ["db", "sql"])
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["messaging", "rabbitmq"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Shipping Service API v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapShippingApiEndpoints();

app.Run();

public partial class Program;

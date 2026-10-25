using Microsoft.OpenApi;
using ShopSphere.BuildingBlocks.Messaging.Configuration;
using ShopSphere.BuildingBlocks.Observability;
using ShopSphere.BuildingBlocks.Persistence.Extensions;
using ShopSphere.BuildingBlocks.Security;
using ShopSphere.Services.Notification.Api.Endpoints;
using ShopSphere.Services.Notification.Api.Health;
using ShopSphere.Services.Notification.Application.Consumers;
using ShopSphere.Services.Notification.Infrastructure;
using ShopSphere.Services.Notification.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ShopSphere Notification Service API",
        Version = "v1",
        Description = "Asynchronous customer notification service for OrderConfirmed and OrderCancelled Email/SMS dispatch."
    });
});

builder.Services.AddShopSphereObservability(builder.Configuration, "NotificationService");
builder.Services.AddShopSphereAuthentication(builder.Configuration);
builder.Services.AddShopSphereAuthorization();
builder.Services.AddNotificationInfrastructure(builder.Configuration);

builder.Services.AddShopSphereMessaging(builder.Configuration, busConfig =>
{
    busConfig.AddConsumer<OrderNotificationConsumer>();
    busConfig.AddShopSphereOutbox<NotificationDbContext>();
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification Service API v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapNotificationApiEndpoints();

app.Run();

public partial class Program;

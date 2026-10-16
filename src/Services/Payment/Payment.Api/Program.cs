using Microsoft.OpenApi.Models;
using ShopSphere.BuildingBlocks.Messaging.Configuration;
using ShopSphere.BuildingBlocks.Observability;
using ShopSphere.BuildingBlocks.Persistence.Extensions;
using ShopSphere.BuildingBlocks.Security;
using ShopSphere.Services.Payment.Api;
using ShopSphere.Services.Payment.Api.Health;
using ShopSphere.Services.Payment.Api.Services;
using ShopSphere.Services.Payment.Application.Consumers;
using ShopSphere.Services.Payment.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ShopSphere Payment Service API",
        Version = "v1",
        Description = "Microservice managing payment processing, mock provider interactions, timeout handling (PaymentUnknown), and webhooks."
    });
});

builder.Services.AddShopSphereObservability(builder.Configuration, "PaymentService");
builder.Services.AddShopSphereAuthentication(builder.Configuration);
builder.Services.AddShopSphereAuthorization();
builder.Services.AddPaymentInfrastructure(builder.Configuration);

builder.Services.AddSingleton<IPaymentWebhookSignatureValidator, PaymentWebhookSignatureValidator>();

builder.Services.AddShopSphereMessaging(builder.Configuration, busConfig =>
{
    busConfig.AddConsumer<CollectPaymentConsumer>();
    busConfig.AddShopSphereOutbox<PaymentDbContext>();
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Payment Service API v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapPaymentEndpoints();

app.Run();

public partial class Program { }

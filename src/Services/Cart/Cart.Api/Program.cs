using Microsoft.OpenApi.Models;
using ShopSphere.BuildingBlocks.Observability;
using ShopSphere.Services.Cart.Api;
using ShopSphere.Services.Cart.Api.Health;
using ShopSphere.Services.Cart.Application;
using ShopSphere.Services.Cart.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ShopSphere Cart Service API",
        Version = "v1",
        Description = "Microservice managing user shopping carts, Redis persistence, live catalog price verification, and checkout quotes."
    });
});

builder.Services.AddShopSphereObservability(builder.Configuration, "CartService");
builder.Services.AddCartApplication();
builder.Services.AddCartInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddCheck<RedisHealthCheck>("redis", tags: ["cache", "redis"])
    .AddCheck<CatalogServiceHealthCheck>("catalog", tags: ["http", "catalog"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Cart Service API v1");
    });
}

app.UseHttpsRedirection();

app.MapCartEndpoints();

app.Run();

public partial class Program { }

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.BuildingBlocks.Observability;
using ShopSphere.Services.Catalog.Api;
using ShopSphere.Services.Catalog.Api.Health;
using ShopSphere.Services.Catalog.Application;
using ShopSphere.Services.Catalog.Application.Common.Interfaces;
using ShopSphere.Services.Catalog.Application.Products.Commands.CreateProduct;
using ShopSphere.Services.Catalog.Application.Products.Commands.UpdatePrice;
using ShopSphere.Services.Catalog.Application.Products.Dtos;
using ShopSphere.Services.Catalog.Application.Products.Queries.GetProductBySku;
using ShopSphere.Services.Catalog.Application.Products.Queries.GetProducts;
using ShopSphere.Services.Catalog.Domain;
using ShopSphere.Services.Catalog.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ShopSphere Catalog Service API",
        Version = "v1",
        Description = "Microservice managing products, prices, SKU validation, and catalog inventory references in ShopSphere."
    });
});

builder.Services.AddShopSphereObservability(builder.Configuration, "CatalogService");
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddCheck<SqlServerHealthCheck>("sqlserver", tags: ["db", "sql"])
    .AddCheck<RedisHealthCheck>("redis", tags: ["cache", "redis"])
    .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["messaging", "rabbitmq"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Catalog Service API v1");
    });
}

app.UseHttpsRedirection();

app.MapCatalogEndpoints();

app.Run();


public record CreateProductRequest(
    string Name,
    string Description,
    string Sku,
    decimal PriceAmount,
    string? Currency,
    bool ActivateImmediately = true);

public record UpdatePriceRequest(
    decimal NewPrice,
    string? Currency);

public record ProductResponse(
    Guid Id,
    string Name,
    string Description,
    string Sku,
    decimal Price,
    string Currency,
    string Status,
    int PriceVersion);

public partial class Program { }

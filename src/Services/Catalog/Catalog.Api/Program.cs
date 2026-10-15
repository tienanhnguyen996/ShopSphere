using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopSphere.Services.Catalog.Application;
using ShopSphere.Services.Catalog.Domain;
using ShopSphere.Services.Catalog.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "CatalogService" }))
   .WithName("HealthCheck")
   .WithOpenApi();

// Get active products
app.MapGet("/api/v1/products", async (CatalogDbContext dbContext, CancellationToken ct) =>
{
    var products = await dbContext.Products
        .AsNoTracking()
        .Where(p => p.Status == ProductStatus.Active)
        .Select(p => new ProductResponse(
            p.Id,
            p.Name,
            p.Description,
            p.Sku.Value,
            p.Price.Amount,
            p.Price.Currency,
            p.Status.ToString(),
            p.PriceVersion))
        .ToListAsync(ct);

    return Results.Ok(products);
})
.WithName("GetProducts")
.WithOpenApi();

// Get product by id
app.MapGet("/api/v1/products/{id:guid}", async (Guid id, CatalogDbContext dbContext, CancellationToken ct) =>
{
    var product = await dbContext.Products
        .AsNoTracking()
        .FirstOrDefaultAsync(p => p.Id == id, ct);

    if (product is null)
    {
        return Results.NotFound(new { error = $"Product with id '{id}' was not found." });
    }

    return Results.Ok(new ProductResponse(
        product.Id,
        product.Name,
        product.Description,
        product.Sku.Value,
        product.Price.Amount,
        product.Price.Currency,
        product.Status.ToString(),
        product.PriceVersion));
})
.WithName("GetProductById")
.WithOpenApi();

// Create product
app.MapPost("/api/v1/products", async (
    [FromBody] CreateProductRequest request,
    CatalogDbContext dbContext,
    CancellationToken ct) =>
{
    var skuExists = await dbContext.Products
        .AnyAsync(p => p.Sku == new Sku(request.Sku), ct);

    if (skuExists)
    {
        return Results.Conflict(new { error = $"Product with SKU '{request.Sku}' already exists." });
    }

    var product = Product.Create(
        Guid.NewGuid(),
        request.Name,
        request.Description,
        new Sku(request.Sku),
        new Money(request.PriceAmount, request.Currency ?? "USD"));

    if (request.ActivateImmediately)
    {
        product.Activate();
    }

    dbContext.Products.Add(product);
    await dbContext.SaveChangesAsync(ct);

    return Results.Created($"/api/v1/products/{product.Id}", new ProductResponse(
        product.Id,
        product.Name,
        product.Description,
        product.Sku.Value,
        product.Price.Amount,
        product.Price.Currency,
        product.Status.ToString(),
        product.PriceVersion));
})
.WithName("CreateProduct")
.WithOpenApi();

// Update product price
app.MapPut("/api/v1/products/{id:guid}/price", async (
    Guid id,
    [FromBody] UpdatePriceRequest request,
    CatalogDbContext dbContext,
    CancellationToken ct) =>
{
    var product = await dbContext.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
    if (product is null)
    {
        return Results.NotFound(new { error = $"Product with id '{id}' was not found." });
    }

    try
    {
        product.UpdatePrice(new Money(request.NewPrice, request.Currency ?? product.Price.Currency));
        await dbContext.SaveChangesAsync(ct);

        return Results.Ok(new ProductResponse(
            product.Id,
            product.Name,
            product.Description,
            product.Sku.Value,
            product.Price.Amount,
            product.Price.Currency,
            product.Status.ToString(),
            product.PriceVersion));
    }
    catch (DbUpdateConcurrencyException)
    {
        return Results.Conflict(new { error = "A concurrency conflict occurred. The product was modified by another operation." });
    }
})
.WithName("UpdateProductPrice")
.WithOpenApi();

app.Run();

public record ProductResponse(
    Guid Id,
    string Name,
    string Description,
    string Sku,
    decimal Price,
    string Currency,
    string Status,
    int PriceVersion);

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

// Expose Program for integration tests
public partial class Program { }

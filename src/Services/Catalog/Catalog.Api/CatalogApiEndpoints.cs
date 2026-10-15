using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Catalog.Application.Common.Interfaces;
using ShopSphere.Services.Catalog.Application.Products.Commands.CreateProduct;
using ShopSphere.Services.Catalog.Application.Products.Commands.UpdatePrice;
using ShopSphere.Services.Catalog.Application.Products.Dtos;
using ShopSphere.Services.Catalog.Application.Products.Queries.GetProductBySku;
using ShopSphere.Services.Catalog.Application.Products.Queries.GetProducts;

namespace ShopSphere.Services.Catalog.Api;

public static class CatalogApiEndpoints
{
    public static WebApplication MapCatalogEndpoints(this WebApplication app)
    {
        // Health check endpoint with diagnostic component breakdown
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var response = new
                {
                    status = report.Status.ToString(),
                    totalDuration = report.TotalDuration.ToString(),
                    entries = report.Entries.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new
                        {
                            status = kvp.Value.Status.ToString(),
                            description = kvp.Value.Description,
                            duration = kvp.Value.Duration.ToString(),
                            error = kvp.Value.Exception?.Message
                        })
                };

                var statusCode = report.Status == HealthStatus.Healthy
                    ? StatusCodes.Status200OK
                    : StatusCodes.Status503ServiceUnavailable;

                context.Response.StatusCode = statusCode;
                await context.Response.WriteAsJsonAsync(response);
            }
        })
        .WithName("HealthCheck")
        .WithTags("Health")
        .WithOpenApi();

        // POST /api/v1/catalog/products
        app.MapPost("/api/v1/catalog/products", async (
            [FromBody] CreateProductRequest request,
            CreateProductCommandHandler handler,
            CancellationToken ct) =>
        {
            var command = new CreateProductCommand(
                request.Name,
                request.Description,
                request.Sku,
                request.PriceAmount,
                request.Currency ?? "USD",
                request.ActivateImmediately);

            var result = await handler.HandleAsync(command, ct);
            return ToHttpResult(result, StatusCodes.Status201Created, $"/api/v1/catalog/products/{result.Match(p => p.Sku, _ => string.Empty)}");
        })
        .WithName("CreateCatalogProduct")
        .WithTags("Products")
        .WithOpenApi();

        // Route alias: POST /api/v1/products
        app.MapPost("/api/v1/products", async (
            [FromBody] CreateProductRequest request,
            CreateProductCommandHandler handler,
            CancellationToken ct) =>
        {
            var command = new CreateProductCommand(
                request.Name,
                request.Description,
                request.Sku,
                request.PriceAmount,
                request.Currency ?? "USD",
                request.ActivateImmediately);

            var result = await handler.HandleAsync(command, ct);
            return ToHttpResult(result, StatusCodes.Status201Created, $"/api/v1/products/{result.Match(p => p.Id.ToString(), _ => string.Empty)}");
        })
        .WithName("CreateProductAlias")
        .WithTags("Products")
        .ExcludeFromDescription();

        // GET /api/v1/catalog/products
        app.MapGet("/api/v1/catalog/products", async (
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] bool? onlyActive,
            IGetProductsQueryHandler handler,
            CancellationToken ct) =>
        {
            var query = new GetProductsQuery(page ?? 1, pageSize ?? 20, onlyActive ?? true);
            var result = await handler.HandleAsync(query, ct);
            return ToHttpResult(result);
        })
        .WithName("GetCatalogProducts")
        .WithTags("Products")
        .WithOpenApi();

        // Route alias: GET /api/v1/products
        app.MapGet("/api/v1/products", async (
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] bool? onlyActive,
            IGetProductsQueryHandler handler,
            CancellationToken ct) =>
        {
            var query = new GetProductsQuery(page ?? 1, pageSize ?? 20, onlyActive ?? true);
            var result = await handler.HandleAsync(query, ct);
            return ToHttpResult(result);
        })
        .WithName("GetProductsAlias")
        .WithTags("Products")
        .ExcludeFromDescription();

        // GET /api/v1/catalog/products/{sku}
        app.MapGet("/api/v1/catalog/products/{sku}", async (
            string sku,
            IGetProductBySkuQueryHandler handler,
            CancellationToken ct) =>
        {
            var query = new GetProductBySkuQuery(sku);
            var result = await handler.HandleAsync(query, ct);
            return ToHttpResult(result);
        })
        .WithName("GetCatalogProductBySku")
        .WithTags("Products")
        .WithOpenApi();

        // Route alias: GET /api/v1/products/{id:guid}
        app.MapGet("/api/v1/products/{id:guid}", async (
            Guid id,
            ICatalogDbContext dbContext,
            CancellationToken ct) =>
        {
            var product = await dbContext.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            if (product is null)
            {
                return Results.NotFound(new { error = $"Product with id '{id}' was not found.", code = "Product.NotFound" });
            }

            var dto = new ProductDto(
                product.Id,
                product.Name,
                product.Description,
                product.Sku.Value,
                product.Price.Amount,
                product.Price.Currency,
                product.Status.ToString(),
                product.PriceVersion,
                product.CreatedAtUtc,
                product.UpdatedAtUtc);

            return Results.Ok(dto);
        })
        .WithName("GetProductByIdAlias")
        .WithTags("Products")
        .ExcludeFromDescription();

        // PUT /api/v1/catalog/products/{sku}/price
        app.MapPut("/api/v1/catalog/products/{sku}/price", async (
            string sku,
            [FromBody] UpdatePriceRequest request,
            UpdateProductPriceCommandHandler handler,
            CancellationToken ct) =>
        {
            var command = new UpdateProductPriceCommand(sku, request.NewPrice, request.Currency);
            var result = await handler.HandleAsync(command, ct);
            return ToHttpResult(result);
        })
        .WithName("UpdateCatalogProductPrice")
        .WithTags("Products")
        .WithOpenApi();

        // Route alias: PUT /api/v1/products/{id:guid}/price
        app.MapPut("/api/v1/products/{id:guid}/price", async (
            Guid id,
            [FromBody] UpdatePriceRequest request,
            ICatalogDbContext dbContext,
            UpdateProductPriceCommandHandler handler,
            CancellationToken ct) =>
        {
            var product = await dbContext.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, ct);

            if (product is null)
            {
                return Results.NotFound(new { error = $"Product with id '{id}' was not found.", code = "Product.NotFound" });
            }

            var command = new UpdateProductPriceCommand(product.Sku.Value, request.NewPrice, request.Currency);
            var result = await handler.HandleAsync(command, ct);
            return ToHttpResult(result);
        })
        .WithName("UpdateProductPriceByIdAlias")
        .WithTags("Products")
        .ExcludeFromDescription();

        return app;
    }

    private static IResult ToHttpResult<T>(Result<T> result, int successStatusCode = StatusCodes.Status200OK, string? locationUri = null)
    {
        if (result.IsSuccess)
        {
            return successStatusCode switch
            {
                StatusCodes.Status201Created => Results.Created(locationUri ?? string.Empty, result.Value),
                StatusCodes.Status204NoContent => Results.NoContent(),
                _ => Results.Ok(result.Value)
            };
        }

        return result.Error.Type switch
        {
            ErrorType.NotFound => Results.NotFound(new { error = result.Error.Description, code = result.Error.Code }),
            ErrorType.Validation => Results.BadRequest(new { error = result.Error.Description, code = result.Error.Code }),
            ErrorType.Conflict => Results.Conflict(new { error = result.Error.Description, code = result.Error.Code }),
            _ => Results.Problem(detail: result.Error.Description, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}

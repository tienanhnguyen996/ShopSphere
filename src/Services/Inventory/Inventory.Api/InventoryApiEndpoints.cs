using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShopSphere.BuildingBlocks.Security;
using ShopSphere.Services.Inventory.Application.Stock.Commands.AdjustStock;
using ShopSphere.Services.Inventory.Application.Stock.Dtos;
using ShopSphere.Services.Inventory.Application.Stock.Queries.GetStockBySku;

namespace ShopSphere.Services.Inventory.Api;

public static class InventoryApiEndpoints
{
    public static WebApplication MapInventoryEndpoints(this WebApplication app)
    {
        // Health check endpoint
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

                var statusCode = report.Status != HealthStatus.Unhealthy
                    ? StatusCodes.Status200OK
                    : StatusCodes.Status503ServiceUnavailable;

                context.Response.StatusCode = statusCode;
                await context.Response.WriteAsJsonAsync(response);
            }
        })
        .WithName("HealthCheck")
        .WithTags("Health")
        .WithOpenApi();

        var group = app.MapGroup("/api/v1/inventory")
            .WithTags("Inventory");

        // POST /api/v1/inventory/adjustments
        group.MapPost("/adjustments", async (
            [FromBody] StockAdjustmentRequest request,
            IAdjustStockCommandHandler handler,
            CancellationToken ct) =>
        {
            var command = new AdjustStockCommand(request.Sku, request.Quantity, request.Reason);
            var result = await handler.HandleAsync(command, ct);

            if (result.IsFailure)
            {
                if (result.Error.Code == "Inventory.NotFound")
                {
                    return Results.NotFound(new
                    {
                        type = "NotFound",
                        code = result.Error.Code,
                        message = result.Error.Description
                    });
                }

                return Results.BadRequest(new
                {
                    type = "Validation",
                    code = result.Error.Code,
                    message = result.Error.Description
                });
            }

            return Results.Ok(ToResponse(result.Value));
        })
        .WithName("AdjustStock")
        .WithSummary("Adjust stock availability for warehouse intake or inventory corrections")
        .Produces<StockResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .RequirePermission(ShopSpherePermissions.Inventory.AdjustStock)
        .WithOpenApi();

        // GET /api/v1/inventory/{sku}
        group.MapGet("/{sku}", async (
            string sku,
            IGetStockBySkuQueryHandler handler,
            CancellationToken ct) =>
        {
            var query = new GetStockBySkuQuery(sku);
            var result = await handler.HandleAsync(query, ct);

            if (result.IsFailure)
            {
                if (result.Error.Code == "Inventory.NotFound")
                {
                    return Results.NotFound(new
                    {
                        type = "NotFound",
                        code = result.Error.Code,
                        message = result.Error.Description
                    });
                }

                return Results.BadRequest(new
                {
                    type = "Validation",
                    code = result.Error.Code,
                    message = result.Error.Description
                });
            }

            return Results.Ok(ToResponse(result.Value));
        })
        .WithName("GetStockBySku")
        .WithSummary("Get real-time stock allocation breakdown for a product SKU")
        .Produces<StockResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        return app;
    }

    private static StockResponse ToResponse(StockDto dto) =>
        new(dto.Id, dto.Sku, dto.QuantityAvailable, dto.QuantityHeld, dto.QuantityCommitted, dto.TotalStock, dto.LastModifiedUtc);
}

public record StockAdjustmentRequest(
    string Sku,
    int Quantity,
    string? Reason);

public record StockResponse(
    Guid Id,
    string Sku,
    int QuantityAvailable,
    int QuantityHeld,
    int QuantityCommitted,
    int TotalStock,
    DateTime? LastModifiedUtc);

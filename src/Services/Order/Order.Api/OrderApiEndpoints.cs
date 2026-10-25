using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.BuildingBlocks.Idempotency;
using ShopSphere.Services.Order.Application.Orders.Commands.CancelOrder;
using ShopSphere.Services.Order.Application.Orders.Commands.SubmitCheckout;
using ShopSphere.Services.Order.Application.Orders.Dtos;
using ShopSphere.Services.Order.Application.Orders.Queries.GetOrderById;
using ShopSphere.Services.Order.Application.Orders.Queries.GetOrders;

namespace ShopSphere.Services.Order.Api;

public static class OrderApiEndpoints
{
    public const string CustomerIdHeader = "X-Customer-Id";
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static WebApplication MapOrderEndpoints(this WebApplication app)
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

                context.Response.StatusCode = report.Status == HealthStatus.Healthy
                    ? StatusCodes.Status200OK
                    : StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsJsonAsync(response);
            }
        })
        .WithName("HealthCheck")
        .WithTags("Health")
        .WithOpenApi();

        var group = app.MapGroup("/api/v1/orders")
            .WithTags("Orders");

        // POST /api/v1/orders/checkout
        group.MapPost("/checkout", async (
            HttpContext context,
            [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
            [FromBody] CheckoutRequest request,
            IIdempotencyStore idempotencyStore,
            ISubmitCheckoutCommandHandler handler,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                return Results.BadRequest(new
                {
                    error = $"The '{IdempotencyKeyHeader}' header is required for checkout.",
                    code = "Order.IdempotencyKeyRequired"
                });
            }

            var customerIdResult = GetCustomerId(context);
            if (customerIdResult.IsFailure)
            {
                return ToHttpResult(customerIdResult);
            }

            var normalizedKey = idempotencyKey.Trim();
            var serializedRequest = JsonSerializer.Serialize(new
            {
                CustomerId = customerIdResult.Value,
                request.PaymentMethod,
                request.ShippingAddress
            }, _jsonOptions);

            var fingerprint = IdempotencyFingerprint.Compute(
                HttpMethods.Post,
                "/api/v1/orders/checkout",
                serializedRequest);

            var existingRecord = await idempotencyStore.GetAsync(normalizedKey, ct);
            if (existingRecord is not null)
            {
                if (string.Equals(existingRecord.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    return Results.Content(
                        existingRecord.ResponseBody ?? string.Empty,
                        "application/json",
                        statusCode: existingRecord.ResponseStatusCode);
                }

                return Results.Conflict(new
                {
                    error = "An order checkout with this Idempotency-Key already exists with a different request payload.",
                    code = "Idempotency.PayloadMismatch"
                });
            }

            var command = new SubmitCheckoutCommand(
                customerIdResult.Value,
                request.ShippingAddress,
                request.PaymentMethod);

            var result = await handler.HandleAsync(command, ct);
            if (result.IsFailure)
            {
                return ToHttpResult(result);
            }

            var responseBody = JsonSerializer.Serialize(result.Value, _jsonOptions);
            await idempotencyStore.SaveAsync(new IdempotencyRecord
            {
                Key = normalizedKey,
                RequestFingerprint = fingerprint,
                ResponseStatusCode = StatusCodes.Status201Created,
                ResponseBody = responseBody,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddHours(24)
            }, ct);

            return Results.Created($"/api/v1/orders/{result.Value.Id}", result.Value);
        })
        .WithName("SubmitCheckout")
        .WithSummary("Submit an idempotent checkout request using a verified Cart quote snapshot")
        .Produces<OrderDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status503ServiceUnavailable)
        .WithOpenApi();

        // GET /api/v1/orders
        group.MapGet("/", async (
            HttpContext context,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            IGetOrdersQueryHandler handler = default!,
            CancellationToken ct = default) =>
        {
            var customerIdResult = GetCustomerId(context);
            if (customerIdResult.IsFailure)
            {
                return ToHttpResult(customerIdResult);
            }

            var query = new GetOrdersQuery(customerIdResult.Value, page, pageSize);
            var result = await handler.HandleAsync(query, ct);
            return ToHttpResult(result);
        })
        .WithName("GetOrders")
        .WithSummary("Retrieve paged orders for the current customer")
        .Produces<PagedOrderResult>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .WithOpenApi();

        // GET /api/v1/orders/{id}
        group.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext context,
            IGetOrderByIdQueryHandler handler,
            CancellationToken ct) =>
        {
            var customerIdResult = GetCustomerId(context);
            var customerId = customerIdResult.IsSuccess ? customerIdResult.Value : null;

            var query = new GetOrderByIdQuery(id, customerId);
            var result = await handler.HandleAsync(query, ct);
            return ToHttpResult(result);
        })
        .WithName("GetOrderById")
        .WithSummary("Retrieve an order by its identifier including status, line items, and shipment tracking")
        .Produces<OrderDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        // POST /api/v1/orders/{id}/cancel
        group.MapPost("/{id:guid}/cancel", async (
            Guid id,
            HttpContext context,
            [FromBody] CancelOrderRequest? request,
            ICancelOrderCommandHandler handler,
            CancellationToken ct) =>
        {
            var customerIdResult = GetCustomerId(context);
            var customerId = customerIdResult.IsSuccess ? customerIdResult.Value : string.Empty;

            var command = new CancelOrderCommand(id, customerId, request?.Reason ?? "Cancelled by customer.");
            var result = await handler.HandleAsync(command, ct);
            return ToHttpResult(result);
        })
        .WithName("CancelOrder")
        .WithSummary("Cancel a pending or in-flight order")
        .Produces<OrderDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict)
        .WithOpenApi();

        return app;
    }

    public static Result<string> GetCustomerId(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var claimValue = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst("sub")?.Value;

            if (!string.IsNullOrWhiteSpace(claimValue))
            {
                return Result.Success(claimValue.Trim());
            }
        }

        if (context.Request.Headers.TryGetValue(CustomerIdHeader, out var customerIdValues))
        {
            var customerId = customerIdValues.ToString();
            if (!string.IsNullOrWhiteSpace(customerId))
            {
                return Result.Success(customerId.Trim());
            }
        }

        return Result.Failure<string>(
            Error.Validation("Order.CustomerIdRequired", $"The '{CustomerIdHeader}' header or authenticated user token is required."));
    }

    public static IResult ToHttpResult<T>(Result<T> result, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return successStatusCode switch
            {
                StatusCodes.Status204NoContent => Results.NoContent(),
                StatusCodes.Status201Created => Results.Created(string.Empty, result.Value),
                _ => Results.Ok(result.Value)
            };
        }

        return result.Error.Type switch
        {
            ErrorType.NotFound => Results.NotFound(new { error = result.Error.Description, code = result.Error.Code }),
            ErrorType.Validation => Results.BadRequest(new { error = result.Error.Description, code = result.Error.Code }),
            ErrorType.Conflict => Results.Conflict(new { error = result.Error.Description, code = result.Error.Code }),
            ErrorType.Failure when result.Error.Code.Contains("Unavailable", StringComparison.OrdinalIgnoreCase) =>
                Results.Problem(detail: result.Error.Description, statusCode: StatusCodes.Status503ServiceUnavailable, title: result.Error.Code),
            _ => Results.Problem(detail: result.Error.Description, statusCode: StatusCodes.Status500InternalServerError, title: result.Error.Code)
        };
    }
}

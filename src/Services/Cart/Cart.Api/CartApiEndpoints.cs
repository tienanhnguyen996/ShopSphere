using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Cart.Application.Carts.Commands.AddItem;
using ShopSphere.Services.Cart.Application.Carts.Commands.ClearCart;
using ShopSphere.Services.Cart.Application.Carts.Commands.RemoveItem;
using ShopSphere.Services.Cart.Application.Carts.Commands.UpdateQuantity;
using ShopSphere.Services.Cart.Application.Carts.Dtos;
using ShopSphere.Services.Cart.Application.Carts.Queries.GetCart;
using ShopSphere.Services.Cart.Application.Carts.Queries.GetCheckoutQuote;

namespace ShopSphere.Services.Cart.Api;

public static class CartApiEndpoints
{
    public const string CustomerIdHeader = "X-Customer-Id";

    public static WebApplication MapCartEndpoints(this WebApplication app)
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

        // GET /api/v1/cart
        app.MapGet("/api/v1/cart", async (
            HttpContext context,
            IGetCartQueryHandler handler,
            CancellationToken ct) =>
        {
            var customerIdResult = GetCustomerId(context);
            if (customerIdResult.IsFailure)
            {
                return ToHttpResult(customerIdResult);
            }

            var query = new GetCartQuery(customerIdResult.Value);
            var result = await handler.HandleAsync(query, ct);
            return ToHttpResult(result);
        })
        .WithName("GetCart")
        .WithTags("Cart")
        .Produces<CartDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        // POST /api/v1/cart/items
        app.MapPost("/api/v1/cart/items", async (
            HttpContext context,
            [FromBody] AddCartItemRequest request,
            IAddItemToCartCommandHandler handler,
            CancellationToken ct) =>
        {
            var customerIdResult = GetCustomerId(context);
            if (customerIdResult.IsFailure)
            {
                return ToHttpResult(customerIdResult);
            }

            var command = new AddItemToCartCommand(customerIdResult.Value, request.Sku, request.Quantity);
            var result = await handler.HandleAsync(command, ct);
            return ToHttpResult(result, StatusCodes.Status200OK);
        })
        .WithName("AddItemToCart")
        .WithTags("Cart")
        .Produces<CartDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        // PUT /api/v1/cart/items/{sku}
        app.MapPut("/api/v1/cart/items/{sku}", async (
            HttpContext context,
            string sku,
            [FromBody] UpdateCartItemQuantityRequest request,
            IUpdateCartItemQuantityCommandHandler handler,
            CancellationToken ct) =>
        {
            var customerIdResult = GetCustomerId(context);
            if (customerIdResult.IsFailure)
            {
                return ToHttpResult(customerIdResult);
            }

            var command = new UpdateCartItemQuantityCommand(customerIdResult.Value, sku, request.Quantity);
            var result = await handler.HandleAsync(command, ct);
            return ToHttpResult(result);
        })
        .WithName("UpdateCartItemQuantity")
        .WithTags("Cart")
        .Produces<CartDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        // DELETE /api/v1/cart/items/{sku}
        app.MapDelete("/api/v1/cart/items/{sku}", async (
            HttpContext context,
            string sku,
            IRemoveCartItemCommandHandler handler,
            CancellationToken ct) =>
        {
            var customerIdResult = GetCustomerId(context);
            if (customerIdResult.IsFailure)
            {
                return ToHttpResult(customerIdResult);
            }

            var command = new RemoveCartItemCommand(customerIdResult.Value, sku);
            var result = await handler.HandleAsync(command, ct);
            return result.IsSuccess
                ? Results.NoContent()
                : ToHttpResult(result);
        })
        .WithName("RemoveCartItem")
        .WithTags("Cart")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        // DELETE /api/v1/cart
        app.MapDelete("/api/v1/cart", async (
            HttpContext context,
            IClearCartCommandHandler handler,
            CancellationToken ct) =>
        {
            var customerIdResult = GetCustomerId(context);
            if (customerIdResult.IsFailure)
            {
                return ToHttpResult(customerIdResult);
            }

            var command = new ClearCartCommand(customerIdResult.Value);
            var result = await handler.HandleAsync(command, ct);
            return result.IsSuccess
                ? Results.NoContent()
                : ToHttpResult(result);
        })
        .WithName("ClearCart")
        .WithTags("Cart")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        // GET /api/v1/cart/quote
        app.MapGet("/api/v1/cart/quote", async (
            HttpContext context,
            IGetCheckoutQuoteQueryHandler handler,
            CancellationToken ct) =>
        {
            var customerIdResult = GetCustomerId(context);
            if (customerIdResult.IsFailure)
            {
                return ToHttpResult(customerIdResult);
            }

            var query = new GetCheckoutQuoteQuery(customerIdResult.Value);
            var result = await handler.HandleAsync(query, ct);
            return ToHttpResult(result);
        })
        .WithName("GetCartCheckoutQuote")
        .WithTags("Cart")
        .Produces<CheckoutQuoteDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status503ServiceUnavailable)
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
            Error.Validation("Cart.CustomerIdRequired", $"The '{CustomerIdHeader}' header is required to access cart operations."));
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

public record AddCartItemRequest(string Sku, int Quantity);
public record UpdateCartItemQuantityRequest(int Quantity);

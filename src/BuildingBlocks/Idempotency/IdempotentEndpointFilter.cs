using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ShopSphere.BuildingBlocks.Idempotency;

public sealed class IdempotentEndpointFilter : IEndpointFilter
{
    public const string HeaderName = "Idempotency-Key";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var keyValues) ||
            string.IsNullOrWhiteSpace(keyValues.ToString()))
        {
            return await next(context);
        }

        var idempotencyKey = keyValues.ToString().Trim();
        var store = httpContext.RequestServices.GetRequiredService<IIdempotencyStore>();

        httpContext.Request.EnableBuffering();
        string body = string.Empty;
        if (httpContext.Request.Body.CanSeek)
        {
            httpContext.Request.Body.Position = 0;
            using var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true);
            body = await reader.ReadToEndAsync();
            httpContext.Request.Body.Position = 0;
        }

        var method = httpContext.Request.Method;
        var path = httpContext.Request.Path.Value ?? string.Empty;
        var fingerprint = IdempotencyFingerprint.Compute(method, path, body);

        var existing = await store.GetAsync(idempotencyKey, httpContext.RequestAborted);
        if (existing is not null)
        {
            if (string.Equals(existing.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            {
                return Results.Content(
                    existing.ResponseBody ?? string.Empty,
                    "application/json",
                    statusCode: existing.ResponseStatusCode);
            }

            return Results.Conflict(new
            {
                title = "Idempotency Conflict",
                status = StatusCodes.Status409Conflict,
                detail = "An operation with this idempotency key already exists with different request parameters."
            });
        }

        var result = await next(context);

        int statusCode = StatusCodes.Status200OK;
        string? serializedBody = null;

        if (result is IStatusCodeHttpResult statusCodeResult && statusCodeResult.StatusCode.HasValue)
        {
            statusCode = statusCodeResult.StatusCode.Value;
        }

        if (result is IValueHttpResult valueResult && valueResult.Value is not null)
        {
            serializedBody = JsonSerializer.Serialize(valueResult.Value);
        }

        var record = new IdempotencyRecord
        {
            Key = idempotencyKey,
            RequestFingerprint = fingerprint,
            ResponseStatusCode = statusCode,
            ResponseBody = serializedBody,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(24)
        };

        await store.SaveAsync(record, httpContext.RequestAborted);

        return result;
    }
}

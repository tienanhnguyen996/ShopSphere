using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ShopSphere.Gateway.RateLimiting;

public static class GatewayRateLimitPolicies
{
    public const string Global = "global";
    public const string AuthStrict = "auth-strict";
    public const string CheckoutStrict = "checkout-strict";
}

public sealed class GatewayRateLimitingOptions
{
    public int GlobalPermitLimit { get; set; } = 100;
    public int AuthPermitLimit { get; set; } = 20;
    public int CheckoutPermitLimit { get; set; } = 30;
    public int WindowSeconds { get; set; } = 60;
}

public static class GatewayRateLimitingExtensions
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static IServiceCollection AddGatewayRateLimiting(
        this IServiceCollection services,
        IConfiguration _)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                var rateLimitConfig = ResolveRateLimitOptions(context.HttpContext);
                var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                    : Math.Max(1, rateLimitConfig.WindowSeconds);

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
                context.HttpContext.Response.ContentType = "application/json";

                var problem = new RateLimitProblemDetails(
                    Type: "https://tools.ietf.org/html/rfc6585#section-4",
                    Title: "Too Many Requests",
                    Status: StatusCodes.Status429TooManyRequests,
                    Detail: $"Rate limit exceeded. Please retry after {retryAfterSeconds} seconds.",
                    Code: "Gateway.RateLimitExceeded",
                    TraceId: Activity.Current?.Id ?? context.HttpContext.TraceIdentifier,
                    RetryAfterSeconds: retryAfterSeconds);

                await JsonSerializer.SerializeAsync(
                    context.HttpContext.Response.Body,
                    problem,
                    _serializerOptions,
                    cancellationToken);
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var rateLimitConfig = ResolveRateLimitOptions(httpContext);
                var window = TimeSpan.FromSeconds(Math.Max(1, rateLimitConfig.WindowSeconds));
                var path = httpContext.Request.Path.Value ?? string.Empty;
                var clientKey = ResolvePartitionKey(httpContext);

                if (path.StartsWith("/api/v1/auth", StringComparison.OrdinalIgnoreCase))
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"auth:{clientKey}:{rateLimitConfig.AuthPermitLimit}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = Math.Max(1, rateLimitConfig.AuthPermitLimit),
                            Window = window,
                            QueueLimit = 0,
                            AutoReplenishment = true
                        });
                }

                if (path.StartsWith("/api/v1/orders/checkout", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/api/v1/payment", StringComparison.OrdinalIgnoreCase))
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"checkout:{clientKey}:{rateLimitConfig.CheckoutPermitLimit}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = Math.Max(1, rateLimitConfig.CheckoutPermitLimit),
                            Window = window,
                            QueueLimit = 0,
                            AutoReplenishment = true
                        });
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    $"global:{clientKey}:{rateLimitConfig.GlobalPermitLimit}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Math.Max(1, rateLimitConfig.GlobalPermitLimit),
                        Window = window,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy(GatewayRateLimitPolicies.Global, httpContext =>
            {
                var rateLimitConfig = ResolveRateLimitOptions(httpContext);
                var window = TimeSpan.FromSeconds(Math.Max(1, rateLimitConfig.WindowSeconds));
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"policy-global:{ResolvePartitionKey(httpContext)}:{rateLimitConfig.GlobalPermitLimit}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Math.Max(1, rateLimitConfig.GlobalPermitLimit),
                        Window = window,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy(GatewayRateLimitPolicies.AuthStrict, httpContext =>
            {
                var rateLimitConfig = ResolveRateLimitOptions(httpContext);
                var window = TimeSpan.FromSeconds(Math.Max(1, rateLimitConfig.WindowSeconds));
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"policy-auth:{ResolvePartitionKey(httpContext)}:{rateLimitConfig.AuthPermitLimit}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Math.Max(1, rateLimitConfig.AuthPermitLimit),
                        Window = window,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy(GatewayRateLimitPolicies.CheckoutStrict, httpContext =>
            {
                var rateLimitConfig = ResolveRateLimitOptions(httpContext);
                var window = TimeSpan.FromSeconds(Math.Max(1, rateLimitConfig.WindowSeconds));
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"policy-checkout:{ResolvePartitionKey(httpContext)}:{rateLimitConfig.CheckoutPermitLimit}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Math.Max(1, rateLimitConfig.CheckoutPermitLimit),
                        Window = window,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        return services;
    }

    private static GatewayRateLimitingOptions ResolveRateLimitOptions(HttpContext httpContext)
    {
        var options = new GatewayRateLimitingOptions();
        var config = httpContext.RequestServices.GetService<IConfiguration>();
        config?.GetSection("RateLimiting").Bind(options);
        return options;
    }

    public static string ResolvePartitionKey(HttpContext context)
    {
        var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User?.FindFirstValue("sub");

        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"user:{userId}";
        }

        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor) &&
            !string.IsNullOrWhiteSpace(forwardedFor.ToString()))
        {
            var firstIp = forwardedFor.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            if (!string.IsNullOrWhiteSpace(firstIp))
            {
                return $"ip:{firstIp}";
            }
        }

        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return $"ip:{remoteIp}";
    }

    private sealed record RateLimitProblemDetails(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("status")] int Status,
        [property: JsonPropertyName("detail")] string Detail,
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("traceId")] string TraceId,
        [property: JsonPropertyName("retryAfterSeconds")] int RetryAfterSeconds);
}

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;

namespace ShopSphere.Gateway.Middleware;

public class GatewayErrorHandlingMiddleware
{
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RequestDelegate _next;

    public GatewayErrorHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        var errorFeature = context.Features.Get<IForwarderErrorFeature>();
        if (errorFeature is not null && errorFeature.Error != ForwarderError.None)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "application/json";

                var problem = new GatewayProblemDetails(
                    Type: "https://tools.ietf.org/html/rfc7231#section-6.6.4",
                    Title: "Service Unavailable",
                    Status: StatusCodes.Status503ServiceUnavailable,
                    Detail: "Downstream service is temporarily unavailable or unreachable.",
                    Code: "Gateway.ServiceUnavailable",
                    TraceId: Activity.Current?.Id ?? context.TraceIdentifier
                );

                await JsonSerializer.SerializeAsync(context.Response.Body, problem, _serializerOptions);
            }
        }
    }

    private sealed record GatewayProblemDetails(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("status")] int Status,
        [property: JsonPropertyName("detail")] string Detail,
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("traceId")] string TraceId);
}

public static class GatewayErrorHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGatewayErrorHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GatewayErrorHandlingMiddleware>();
    }
}

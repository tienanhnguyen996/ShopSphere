using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Transforms;

namespace ShopSphere.Gateway.Middleware;

public sealed class GatewayTraceAndSecurityHeadersMiddleware(RequestDelegate next)
{
    public const string CorrelationIdHeader = "X-Correlation-Id";
    public const string TraceIdHeader = "X-Trace-Id";
    public const string TraceParentHeader = "traceparent";
    public const string TraceStateHeader = "tracestate";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveOrCreateCorrelationId(context);
        context.Request.Headers[CorrelationIdHeader] = correlationId;

        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        if (!context.Request.Headers.ContainsKey(TraceParentHeader) && !string.IsNullOrWhiteSpace(Activity.Current?.Id))
        {
            context.Request.Headers[TraceParentHeader] = Activity.Current.Id;
        }

        ApplySecurityAndTraceHeaders(context.Response.Headers, correlationId, traceId);

        context.Response.OnStarting(() =>
        {
            ApplySecurityAndTraceHeaders(context.Response.Headers, correlationId, traceId);
            return Task.CompletedTask;
        });

        await next(context);
    }

    private static string ResolveOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var existing) &&
            !string.IsNullOrWhiteSpace(existing.ToString()))
        {
            return existing.ToString().Trim();
        }

        return Guid.NewGuid().ToString("N");
    }

    private static void ApplySecurityAndTraceHeaders(
        IHeaderDictionary headers,
        string correlationId,
        string traceId)
    {
        headers[CorrelationIdHeader] = correlationId;
        headers[TraceIdHeader] = traceId;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["X-Permitted-Cross-Domain-Policies"] = "none";
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    }
}

public static class GatewayTraceAndSecurityHeadersExtensions
{
    public static IApplicationBuilder UseGatewayTraceAndSecurityHeaders(this IApplicationBuilder app) =>
        app.UseMiddleware<GatewayTraceAndSecurityHeadersMiddleware>();

    public static IReverseProxyBuilder AddGatewayHeaderForwardingTransforms(this IReverseProxyBuilder builder) =>
        builder.AddTransforms(transformContext =>
        {
            transformContext.AddRequestTransform(ctx =>
            {
                if (ctx.HttpContext.Request.Headers.TryGetValue(
                    GatewayTraceAndSecurityHeadersMiddleware.CorrelationIdHeader,
                    out var correlationId))
                {
                    ctx.ProxyRequest.Headers.Remove(GatewayTraceAndSecurityHeadersMiddleware.CorrelationIdHeader);
                    ctx.ProxyRequest.Headers.TryAddWithoutValidation(
                        GatewayTraceAndSecurityHeadersMiddleware.CorrelationIdHeader,
                        correlationId.ToArray());
                }

                var traceParent = Activity.Current?.Id
                    ?? ctx.HttpContext.Request.Headers[GatewayTraceAndSecurityHeadersMiddleware.TraceParentHeader].FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(traceParent))
                {
                    ctx.ProxyRequest.Headers.Remove(GatewayTraceAndSecurityHeadersMiddleware.TraceParentHeader);
                    ctx.ProxyRequest.Headers.TryAddWithoutValidation(
                        GatewayTraceAndSecurityHeadersMiddleware.TraceParentHeader,
                        traceParent);
                }

                if (ctx.HttpContext.Request.Headers.TryGetValue(
                    GatewayTraceAndSecurityHeadersMiddleware.TraceStateHeader,
                    out var traceState) && !string.IsNullOrWhiteSpace(traceState.ToString()))
                {
                    ctx.ProxyRequest.Headers.Remove(GatewayTraceAndSecurityHeadersMiddleware.TraceStateHeader);
                    ctx.ProxyRequest.Headers.TryAddWithoutValidation(
                        GatewayTraceAndSecurityHeadersMiddleware.TraceStateHeader,
                        traceState.ToArray());
                }

                return ValueTask.CompletedTask;
            });
        });
}

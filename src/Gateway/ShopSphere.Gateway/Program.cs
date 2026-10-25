using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using ShopSphere.BuildingBlocks.Observability;
using ShopSphere.BuildingBlocks.Security;
using ShopSphere.Gateway.Middleware;
using ShopSphere.Gateway.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

const string frontendCorsPolicy = "FrontendCorsPolicy";

builder.Services.AddCors(options =>
{
    options.AddPolicy(frontendCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
            .AllowAnyHeader()
            .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
            .AllowCredentials();
    });
});

builder.Services.AddShopSphereAuthentication(builder.Configuration);
builder.Services.AddShopSphereAuthorization();
builder.Services.AddGatewayRateLimiting(builder.Configuration);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddGatewayHeaderForwardingTransforms();

builder.Services.AddShopSphereObservability(builder.Configuration, "ShopSphere.Gateway");

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseGatewayTraceAndSecurityHeaders();
app.UseCors(frontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseGatewayErrorHandling();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString()
        });
    }
});

app.MapReverseProxy();

app.Run();

public partial class Program
{
}

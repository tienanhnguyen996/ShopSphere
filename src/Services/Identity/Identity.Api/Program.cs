using Microsoft.OpenApi.Models;
using ShopSphere.BuildingBlocks.Observability;
using ShopSphere.BuildingBlocks.Security;
using ShopSphere.Services.Identity.Api;
using ShopSphere.Services.Identity.Api.Health;
using ShopSphere.Services.Identity.Application;
using ShopSphere.Services.Identity.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ShopSphere Identity Service API",
        Version = "v1",
        Description = "Microservice managing authentication, JWT tokens, RBAC roles and permissions, and user accounts in ShopSphere."
    });
});

builder.Services.AddShopSphereObservability(builder.Configuration, "IdentityService");
builder.Services.AddApplication();
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddShopSphereAuthentication(builder.Configuration);
builder.Services.AddShopSphereAuthorization();

builder.Services.AddHealthChecks()
    .AddCheck<SqlServerHealthCheck>("sqlserver", tags: ["db", "sql"]);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Identity Service API v1");
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityEndpoints();

app.Run();

public partial class Program { }

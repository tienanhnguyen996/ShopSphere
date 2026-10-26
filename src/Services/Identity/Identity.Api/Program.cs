using Microsoft.OpenApi;
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

    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ShopSphere.Services.Identity.Application.Common.Interfaces.IIdentityDbContext>();
        var hasher = scope.ServiceProvider.GetService<ShopSphere.Services.Identity.Application.Common.Interfaces.IPasswordHashService>();
        if (dbContext is IdentityDbContext efContext)
        {
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(efContext.Database);
            await IdentityDbSeeder.SeedAsync(dbContext, hasher, app.Configuration);
        }
    }
    catch
    {
        // Ignore seeding errors in isolated unit test environments without SQL Server
    }
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityEndpoints();

app.Run();

public partial class Program { }

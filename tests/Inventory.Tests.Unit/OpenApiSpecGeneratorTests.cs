using System.IO;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Moq;
using ShopSphere.Services.Inventory.Api;
using ShopSphere.Services.Inventory.Application;
using ShopSphere.Services.Inventory.Application.Common.Interfaces;
using ShopSphere.Services.Inventory.Infrastructure;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace ShopSphere.Services.Inventory.Tests.Unit;

public class OpenApiSpecGeneratorTests
{
    [Fact]
    public async Task GenerateOpenApiSpec_ShouldProduceValidOpenApiDocumentAndSaveFiles()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ShopSphere Inventory Service API",
                Version = "v1",
                Description = "Microservice managing stock allocations, reservation holds, commit workflows, and intake adjustments."
            });
        });

        builder.Services.AddApplication();

        var dbOptions = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlServer("Server=localhost;Database=TestInventory;User Id=sa;Password=Password123!;TrustServerCertificate=True;")
            .Options;
        var dbContext = new InventoryDbContext(dbOptions);
        builder.Services.AddSingleton<IInventoryDbContext>(dbContext);
        builder.Services.AddSingleton(dbContext);
        builder.Services.AddHealthChecks();

        var app = builder.Build();
        app.MapInventoryEndpoints();
        await app.StartAsync();

        // Act
        var swaggerProvider = app.Services.GetRequiredService<ISwaggerProvider>();
        var swaggerDoc = swaggerProvider.GetSwagger("v1");

        // Assert
        swaggerDoc.Should().NotBeNull();
        swaggerDoc.Info.Title.Should().Be("ShopSphere Inventory Service API");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/inventory/adjustments");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/inventory/{sku}");

        var json = swaggerDoc.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
        json.Should().NotBeNullOrWhiteSpace();

        await app.StopAsync();

        // Write to target paths
        var repoRoot = FindRepoRoot();
        var apiPath = Path.Combine(repoRoot, "src", "Services", "Inventory", "Inventory.Api", "openapi.json");
        var specsDir = Path.Combine(repoRoot, "docs", "specs");
        Directory.CreateDirectory(specsDir);
        var specPath = Path.Combine(specsDir, "inventory-openapi.json");

        File.WriteAllText(apiPath, json);
        File.WriteAllText(specPath, json);

        File.Exists(apiPath).Should().BeTrue();
        File.Exists(specPath).Should().BeTrue();
    }

    private static string FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "ShopSphere.sln")))
        {
            var parent = Directory.GetParent(dir);
            if (parent is null)
            {
                break;
            }

            dir = parent.FullName;
        }

        return dir;
    }
}

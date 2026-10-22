using System.IO;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using ShopSphere.Services.Catalog.Api;
using ShopSphere.Services.Catalog.Application;
using ShopSphere.Services.Catalog.Application.Common.Interfaces;
using ShopSphere.Services.Catalog.Infrastructure;
using ShopSphere.Services.Catalog.Infrastructure.Caching;
using Swashbuckle.AspNetCore.Swagger;

namespace ShopSphere.Services.Catalog.Tests.Unit;

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
                Title = "ShopSphere Catalog Service API",
                Version = "v1",
                Description = "Microservice managing products, prices, SKU validation, and catalog inventory references in ShopSphere."
            });
            c.CustomSchemaIds(type => type == typeof(ShopSphere.Services.Catalog.Application.Common.Models.PagedResult<ShopSphere.Services.Catalog.Application.Products.Dtos.ProductDto>) ? "PagedProductResult" : type.Name);
        });

        builder.Services.AddApplication();
        builder.Services.AddDbContext<CatalogDbContext>(options =>
            options.UseSqlServer("Server=localhost;Database=TestCatalog;TrustServerCertificate=True;"));
        builder.Services.AddScoped<ICatalogDbContext>(sp => sp.GetRequiredService<CatalogDbContext>());
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSingleton<ICacheService, DistributedCacheService>();
        builder.Services.AddHealthChecks();

        var app = builder.Build();
        app.MapCatalogEndpoints();
        await app.StartAsync();

        // Act
        var swaggerProvider = app.Services.GetRequiredService<ISwaggerProvider>();
        var swaggerDoc = swaggerProvider.GetSwagger("v1");

        // Assert
        swaggerDoc.Should().NotBeNull();
        swaggerDoc.Info.Title.Should().Be("ShopSphere Catalog Service API");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/catalog/products");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/catalog/products/{sku}");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/catalog/products/{sku}/price");

        var json = await swaggerDoc.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_0);
        json.Should().NotBeNullOrWhiteSpace();

        await app.StopAsync();

        // Write to target paths
        var repoRoot = FindRepoRoot();
        var apiPath = Path.Combine(repoRoot, "src", "Services", "Catalog", "Catalog.Api", "openapi.json");
        var specsDir = Path.Combine(repoRoot, "docs", "specs");
        Directory.CreateDirectory(specsDir);
        var specPath = Path.Combine(specsDir, "catalog-openapi.json");

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

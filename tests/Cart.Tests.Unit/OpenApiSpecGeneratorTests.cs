using System.IO;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Moq;
using ShopSphere.Services.Cart.Api;
using ShopSphere.Services.Cart.Application;
using ShopSphere.Services.Cart.Application.Common.Interfaces;
using ShopSphere.Services.Cart.Infrastructure.Repositories;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace ShopSphere.Services.Cart.Tests.Unit;

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
                Title = "ShopSphere Cart Service API",
                Version = "v1",
                Description = "Microservice managing user shopping carts, Redis persistence, live catalog price verification, and checkout quotes."
            });
        });

        builder.Services.AddCartApplication();
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddScoped<ICartRepository, RedisCartRepository>();
        builder.Services.AddSingleton<ICatalogClient>(new Mock<ICatalogClient>().Object);
        builder.Services.AddHealthChecks();

        var app = builder.Build();
        app.MapCartEndpoints();
        await app.StartAsync();

        // Act
        var swaggerProvider = app.Services.GetRequiredService<ISwaggerProvider>();
        var swaggerDoc = swaggerProvider.GetSwagger("v1");

        // Assert
        swaggerDoc.Should().NotBeNull();
        swaggerDoc.Info.Title.Should().Be("ShopSphere Cart Service API");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/cart");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/cart/items");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/cart/items/{sku}");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/cart/quote");

        var json = await swaggerDoc.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_0);
        json.Should().NotBeNullOrWhiteSpace();

        await app.StopAsync();

        // Write to target paths
        var repoRoot = FindRepoRoot();
        var apiPath = Path.Combine(repoRoot, "src", "Services", "Cart", "Cart.Api", "openapi.json");
        var specsDir = Path.Combine(repoRoot, "docs", "specs");
        Directory.CreateDirectory(specsDir);
        var specPath = Path.Combine(specsDir, "cart-openapi.json");

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

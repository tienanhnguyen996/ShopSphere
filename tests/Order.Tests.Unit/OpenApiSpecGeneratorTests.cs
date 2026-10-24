using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Moq;
using ShopSphere.BuildingBlocks.Idempotency;
using ShopSphere.Services.Order.Api;
using ShopSphere.Services.Order.Application;
using ShopSphere.Services.Order.Application.Common.Interfaces;
using ShopSphere.Services.Order.Infrastructure;
using ShopSphere.Services.Order.Infrastructure.Idempotency;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace ShopSphere.Services.Order.Tests.Unit;

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
                Title = "ShopSphere Order Service API",
                Version = "v1",
                Description = "Microservice managing customer orders, checkout price snapshots, idempotency, and the MassTransit Checkout State Machine Saga."
            });
        });

        var dbOptions = new DbContextOptionsBuilder<OrderDbContext>()
            .UseSqlServer("Server=localhost;Database=TestOrder;User Id=sa;Password=DevPassword123!;TrustServerCertificate=True;")
            .Options;
        var dbContext = new OrderDbContext(dbOptions);
        builder.Services.AddSingleton<IOrderDbContext>(dbContext);
        builder.Services.AddSingleton(dbContext);
        builder.Services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();
        builder.Services.AddSingleton(new Mock<ICartClient>().Object);
        builder.Services.AddSingleton(new Mock<IPublishEndpoint>().Object);
        builder.Services.AddOrderApplication();
        builder.Services.AddHealthChecks();

        var app = builder.Build();
        app.MapOrderEndpoints();
        await app.StartAsync();

        // Act
        var swaggerProvider = app.Services.GetRequiredService<ISwaggerProvider>();
        var swaggerDoc = swaggerProvider.GetSwagger("v1");

        // Assert
        swaggerDoc.Should().NotBeNull();
        swaggerDoc.Info.Title.Should().Be("ShopSphere Order Service API");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/orders/checkout");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/orders");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/orders/{id}");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/orders/{id}/cancel");

        var json = await swaggerDoc.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_0);
        json.Should().NotBeNullOrWhiteSpace();

        await app.StopAsync();

        var repoRoot = FindRepoRoot();
        var apiPath = Path.Combine(repoRoot, "src", "Services", "Order", "Order.Api", "openapi.json");
        var specsDir = Path.Combine(repoRoot, "docs", "specs");
        Directory.CreateDirectory(specsDir);
        var specPath = Path.Combine(specsDir, "order-openapi.json");

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

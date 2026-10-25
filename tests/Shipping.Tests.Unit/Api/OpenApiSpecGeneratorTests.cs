using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Moq;
using ShopSphere.Services.Shipping.Api.Endpoints;
using ShopSphere.Services.Shipping.Application.Abstractions;
using ShopSphere.Services.Shipping.Application.Commands;
using ShopSphere.Services.Shipping.Application.Queries;
using ShopSphere.Services.Shipping.Infrastructure.Persistence;
using Swashbuckle.AspNetCore.Swagger;

namespace ShopSphere.Services.Shipping.Tests.Unit.Api;

public class OpenApiSpecGeneratorTests
{
    [Fact]
    public async Task GenerateOpenApiSpec_ShouldProduceValidOpenApiDocumentAndSaveFiles()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ShopSphere Shipping Service API",
                Version = "v1",
                Description = "Microservice managing shipment creation, carrier tracking labels, physical carrier handover, and delivery milestones."
            });
        });

        var dbOptions = new DbContextOptionsBuilder<ShippingDbContext>()
            .UseSqlServer("Server=localhost;Database=TestShipping;User Id=sa;Password=DevPassword123!;TrustServerCertificate=True;")
            .Options;
        var dbContext = new ShippingDbContext(dbOptions);
        builder.Services.AddSingleton<IShippingDbContext>(dbContext);
        builder.Services.AddSingleton(dbContext);
        builder.Services.AddSingleton(new Mock<IPublishEndpoint>().Object);
        builder.Services.AddScoped<RecordCarrierHandoverCommandHandler>();
        builder.Services.AddScoped<RecordDeliveryCommandHandler>();
        builder.Services.AddScoped<CancelShipmentCommandHandler>();
        builder.Services.AddScoped<GetShipmentQueriesHandler>();

        var app = builder.Build();
        app.MapShippingApiEndpoints();
        await app.StartAsync();

        var swaggerProvider = app.Services.GetRequiredService<ISwaggerProvider>();
        var swaggerDoc = swaggerProvider.GetSwagger("v1");

        swaggerDoc.Should().NotBeNull();
        swaggerDoc.Info.Title.Should().Be("ShopSphere Shipping Service API");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/shipments/{shipmentId}");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/shipments/orders/{orderId}");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/shipments/tracking/{trackingNumber}");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/shipments/{shipmentId}/handover");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/shipments/{shipmentId}/deliver");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/shipments/{shipmentId}/cancel");

        var json = await swaggerDoc.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_0);
        json.Should().NotBeNullOrWhiteSpace();

        await app.StopAsync();

        var repoRoot = FindRepoRoot();
        var apiPath = Path.Combine(repoRoot, "src", "Services", "Shipping", "Shipping.Api", "openapi.json");
        var specsDir = Path.Combine(repoRoot, "docs", "specs");
        Directory.CreateDirectory(specsDir);
        var specPath = Path.Combine(specsDir, "shipping-openapi.json");

        await File.WriteAllTextAsync(apiPath, json + Environment.NewLine);
        await File.WriteAllTextAsync(specPath, json + Environment.NewLine);

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

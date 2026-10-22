using System.IO;
using FluentAssertions;
using MassTransit;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Moq;
using ShopSphere.Services.Payment.Api;
using ShopSphere.Services.Payment.Api.Services;
using ShopSphere.Services.Payment.Application.Common.Interfaces;
using ShopSphere.Services.Payment.Infrastructure;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace ShopSphere.Services.Payment.Tests.Unit;

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
                Title = "ShopSphere Payment Service API",
                Version = "v1",
                Description = "Microservice managing payment processing, mock provider interactions, timeout handling (PaymentUnknown), and webhooks."
            });
        });

        var dbOptions = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer("Server=localhost;Database=TestPayment;User Id=sa;Password=Password123!;TrustServerCertificate=True;")
            .Options;
        var dbContext = new PaymentDbContext(dbOptions);
        builder.Services.AddSingleton<IPaymentDbContext>(dbContext);
        builder.Services.AddSingleton(dbContext);
        builder.Services.AddSingleton<IPaymentWebhookSignatureValidator, PaymentWebhookSignatureValidator>();
        builder.Services.AddSingleton(new Mock<IPublishEndpoint>().Object);
        builder.Services.AddHealthChecks();

        var app = builder.Build();
        app.MapPaymentEndpoints();
        await app.StartAsync();

        // Act
        var swaggerProvider = app.Services.GetRequiredService<ISwaggerProvider>();
        var swaggerDoc = swaggerProvider.GetSwagger("v1");

        // Assert
        swaggerDoc.Should().NotBeNull();
        swaggerDoc.Info.Title.Should().Be("ShopSphere Payment Service API");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/payment/webhooks/mock-provider");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/payment/attempts/{id}");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/payment/orders/{orderId}");

        var json = await swaggerDoc.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_0);
        json.Should().NotBeNullOrWhiteSpace();

        await app.StopAsync();

        // Write to target paths
        var repoRoot = FindRepoRoot();
        var apiPath = Path.Combine(repoRoot, "src", "Services", "Payment", "Payment.Api", "openapi.json");
        var specsDir = Path.Combine(repoRoot, "docs", "specs");
        Directory.CreateDirectory(specsDir);
        var specPath = Path.Combine(specsDir, "payment-openapi.json");

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

using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using ShopSphere.Services.Notification.Api.Endpoints;
using ShopSphere.Services.Notification.Application.Abstractions;
using ShopSphere.Services.Notification.Application.Commands;
using ShopSphere.Services.Notification.Application.Queries;
using ShopSphere.Services.Notification.Infrastructure.Persistence;
using ShopSphere.Services.Notification.Infrastructure.Senders;
using Swashbuckle.AspNetCore.Swagger;

namespace ShopSphere.Services.Notification.Tests.Unit.Api;

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
                Title = "ShopSphere Notification Service API",
                Version = "v1",
                Description = "Asynchronous customer notification service for OrderConfirmed and OrderCancelled Email/SMS dispatch."
            });
        });

        var dbOptions = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseSqlServer("Server=localhost;Database=TestNotification;User Id=sa;Password=DevPassword123!;TrustServerCertificate=True;")
            .Options;
        var dbContext = new NotificationDbContext(dbOptions);
        builder.Services.AddSingleton<INotificationDbContext>(dbContext);
        builder.Services.AddSingleton(dbContext);
        builder.Services.AddSingleton<INotificationSender, MockNotificationSender>();
        builder.Services.AddScoped<RetryNotificationCommandHandler>();
        builder.Services.AddScoped<GetNotificationQueriesHandler>();

        var app = builder.Build();
        app.MapNotificationApiEndpoints();
        await app.StartAsync();

        var swaggerProvider = app.Services.GetRequiredService<ISwaggerProvider>();
        var swaggerDoc = swaggerProvider.GetSwagger("v1");

        swaggerDoc.Should().NotBeNull();
        swaggerDoc.Info.Title.Should().Be("ShopSphere Notification Service API");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/notifications/{id}");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/notifications/orders/{orderId}");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/notifications/{id}/retry");

        var json = await swaggerDoc.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_0);
        json.Should().NotBeNullOrWhiteSpace();

        await app.StopAsync();

        var repoRoot = FindRepoRoot();
        var apiPath = Path.Combine(repoRoot, "src", "Services", "Notification", "Notification.Api", "openapi.json");
        var specsDir = Path.Combine(repoRoot, "docs", "specs");
        Directory.CreateDirectory(specsDir);
        var specPath = Path.Combine(specsDir, "notification-openapi.json");

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

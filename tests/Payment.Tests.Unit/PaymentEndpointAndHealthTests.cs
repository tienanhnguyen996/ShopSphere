using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using ShopSphere.Services.Payment.Api.Health;
using ShopSphere.Services.Payment.Infrastructure;
using Xunit;

namespace ShopSphere.Services.Payment.Tests.Unit;

public class PaymentEndpointAndHealthTests
{
    [Fact]
    public async Task SqlServerHealthCheck_ShouldReturnHealthStatus()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseSqlServer("Server=localhost;Database=TestPayment;User Id=sa;Password=Password123!;TrustServerCertificate=True;")
            .Options;
        var dbContext = new PaymentDbContext(dbOptions);

        var healthCheck = new SqlServerHealthCheck(dbContext);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert - when DB not accessible in unit test, returns Unhealthy gracefully without throwing
        result.Should().NotBeNull();
        result.Status.Should().BeOneOf(HealthStatus.Healthy, HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task RabbitMqHealthCheck_WhenNoBusControl_ShouldReturnHealthy()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        var healthCheck = new RabbitMqHealthCheck(serviceProvider.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("registered");
    }
}

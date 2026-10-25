using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Catalog.Api.Health;
using ShopSphere.Services.Catalog.Application.Products.Dtos;
using ShopSphere.Services.Catalog.Infrastructure;

namespace ShopSphere.Services.Catalog.Tests.Unit;

public class CatalogEndpointAndHealthTests
{
    #region Health Check Tests

    [Fact]
    public async Task RedisHealthCheck_WhenCacheResponsive_ShouldReturnHealthy()
    {
        // Arrange
        var cacheMock = new Mock<IDistributedCache>();
        cacheMock
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("healthy"u8.ToArray());

        var healthCheck = new RedisHealthCheck(cacheMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("responsive");
    }

    [Fact]
    public async Task RedisHealthCheck_WhenCacheThrows_ShouldReturnUnhealthy()
    {
        // Arrange
        var cacheMock = new Mock<IDistributedCache>();
        cacheMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis down"));

        var healthCheck = new RedisHealthCheck(cacheMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("unavailable");
    }

    [Fact]
    public async Task RabbitMqHealthCheck_WhenBusNullOrRegistered_ShouldReturnHealthy()
    {
        // Arrange
        var spMock = new Mock<IServiceProvider>();
        var healthCheck = new RabbitMqHealthCheck(spMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task SqlServerHealthCheck_WhenConnectionFails_ShouldReturnUnhealthy()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlServer("Server=invalid_nonexistent_host,9999;Database=Test;Connect Timeout=1;")
            .Options;
        var dbContext = new CatalogDbContext(options);

        var healthCheck = new SqlServerHealthCheck(dbContext);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert: must return Unhealthy or Degraded with component failure diagnostic
        result.Status.Should().BeOneOf(HealthStatus.Degraded, HealthStatus.Unhealthy);
        result.Description.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Result Mapping Tests

    [Fact]
    public void ResultMapping_WhenNotFound_ShouldMapTo404()
    {
        // Arrange
        var result = Result.Failure<ProductDto>(Error.NotFound("Product.NotFound", "Product was not found."));

        // Act
        var httpResult = MapResult(result);

        // Assert
        var statusResult = httpResult as IStatusCodeHttpResult;
        statusResult.Should().NotBeNull();
        statusResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void ResultMapping_WhenValidation_ShouldMapTo400()
    {
        // Arrange
        var result = Result.Failure<ProductDto>(Error.Validation("Product.Validation", "Invalid SKU format."));

        // Act
        var httpResult = MapResult(result);

        // Assert
        var statusResult = httpResult as IStatusCodeHttpResult;
        statusResult.Should().NotBeNull();
        statusResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void ResultMapping_WhenConflict_ShouldMapTo409()
    {
        // Arrange
        var result = Result.Failure<ProductDto>(Error.Conflict("Product.Conflict", "SKU already exists."));

        // Act
        var httpResult = MapResult(result);

        // Assert
        var statusResult = httpResult as IStatusCodeHttpResult;
        statusResult.Should().NotBeNull();
        statusResult!.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void ResultMapping_WhenSuccess_ShouldMapTo200Or201()
    {
        // Arrange
        var dto = new ProductDto(Guid.NewGuid(), "Keyboard", "Desc", "KB-001", 99m, "USD", "Active", 1, DateTime.UtcNow, null);
        var result = Result.Success(dto);

        // Act
        var okResult = MapResult(result, StatusCodes.Status200OK);
        var createdResult = MapResult(result, StatusCodes.Status201Created, "/api/v1/catalog/products/KB-001");

        // Assert
        (okResult as IStatusCodeHttpResult)!.StatusCode.Should().Be(StatusCodes.Status200OK);
        (createdResult as IStatusCodeHttpResult)!.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    private static IResult MapResult<T>(Result<T> result, int successStatusCode = StatusCodes.Status200OK, string? locationUri = null)
    {
        if (result.IsSuccess)
        {
            return successStatusCode switch
            {
                StatusCodes.Status201Created => Results.Created(locationUri ?? string.Empty, result.Value),
                StatusCodes.Status204NoContent => Results.NoContent(),
                _ => Results.Ok(result.Value)
            };
        }

        return result.Error.Type switch
        {
            ErrorType.NotFound => Results.NotFound(new { error = result.Error.Description, code = result.Error.Code }),
            ErrorType.Validation => Results.BadRequest(new { error = result.Error.Description, code = result.Error.Code }),
            ErrorType.Conflict => Results.Conflict(new { error = result.Error.Description, code = result.Error.Code }),
            _ => Results.Problem(detail: result.Error.Description, statusCode: StatusCodes.Status500InternalServerError)
        };
    }

    #endregion
}

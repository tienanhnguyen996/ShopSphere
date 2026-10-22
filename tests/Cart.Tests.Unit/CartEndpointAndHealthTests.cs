using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Cart.Api;
using ShopSphere.Services.Cart.Api.Health;
using ShopSphere.Services.Cart.Application.Common.Interfaces;
using Xunit;

namespace ShopSphere.Services.Cart.Tests.Unit;

public class CartEndpointAndHealthTests
{
    #region Health Check Tests

    [Fact]
    public async Task RedisHealthCheck_WhenCacheResponsive_ShouldReturnHealthy()
    {
        // Arrange
        var cacheMock = new Mock<IDistributedCache>();
        cacheMock
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("pong"u8.ToArray());

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
    public async Task CatalogServiceHealthCheck_WhenCatalogAvailable_ShouldReturnHealthy()
    {
        // Arrange
        var catalogMock = new Mock<ICatalogClient>();
        catalogMock
            .Setup(x => x.GetProductBySkuAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CatalogProductDto>(Error.NotFound("Catalog.NotFound", "not found")));

        var healthCheck = new CatalogServiceHealthCheck(catalogMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CatalogServiceHealthCheck_WhenCatalogUnavailable_ShouldReturnUnhealthy()
    {
        // Arrange
        var catalogMock = new Mock<ICatalogClient>();
        catalogMock
            .Setup(x => x.GetProductBySkuAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CatalogProductDto>(Error.Failure("Cart.CatalogUnavailable", "Down")));

        var healthCheck = new CatalogServiceHealthCheck(catalogMock.Object);
        var context = new HealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    #endregion

    #region Header Extraction & Result Mapping Tests

    [Fact]
    public void GetCustomerId_WhenHeaderPresent_ShouldReturnCustomerId()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Customer-Id"] = "cust-42";

        // Act
        var result = CartApiEndpoints.GetCustomerId(httpContext);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("cust-42");
    }

    [Fact]
    public void GetCustomerId_WhenHeaderMissingAndUserUnauthenticated_ShouldReturnValidationFailure()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();

        // Act
        var result = CartApiEndpoints.GetCustomerId(httpContext);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Cart.CustomerIdRequired");
    }

    [Fact]
    public void GetCustomerId_WhenUserAuthenticatedWithSubClaim_ShouldReturnClaimValue()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var claims = new[] { new Claim("sub", "cust-sub-123") };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        // Act
        var result = CartApiEndpoints.GetCustomerId(httpContext);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("cust-sub-123");
    }

    [Fact]
    public void GetCustomerId_WhenUserAuthenticatedWithNameIdentifierClaim_ShouldReturnClaimValue()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "cust-nameid-456") };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        // Act
        var result = CartApiEndpoints.GetCustomerId(httpContext);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("cust-nameid-456");
    }

    [Fact]
    public void GetCustomerId_WhenUserAuthenticatedAndHeaderPresent_ShouldPrioritizeClaimOverHeader()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Customer-Id"] = "guest-header-id";
        var claims = new[] { new Claim("sub", "authenticated-cust-789") };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        // Act
        var result = CartApiEndpoints.GetCustomerId(httpContext);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("authenticated-cust-789");
    }

    [Fact]
    public void ToHttpResult_WhenUnavailableFailure_ShouldReturn503()
    {
        // Arrange
        var result = Result.Failure<string>(Error.Failure("Cart.CatalogUnavailable", "Down"));

        // Act
        var httpResult = CartApiEndpoints.ToHttpResult(result);

        // Assert
        var problem = httpResult as IStatusCodeHttpResult;
        problem.Should().NotBeNull();
        problem!.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public void ToHttpResult_WhenValidation_ShouldReturn400()
    {
        // Arrange
        var result = Result.Failure<string>(Error.Validation("Cart.Validation", "Invalid"));

        // Act
        var httpResult = CartApiEndpoints.ToHttpResult(result);

        // Assert
        var badRequest = httpResult as IStatusCodeHttpResult;
        badRequest.Should().NotBeNull();
        badRequest!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    #endregion
}

using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using ShopSphere.Services.Cart.Domain;
using ShopSphere.Services.Cart.Infrastructure.Repositories;
using Xunit;
using CartAggregate = ShopSphere.Services.Cart.Domain.Cart;

namespace ShopSphere.Services.Cart.Tests.Unit;

public class RedisCartRepositoryTests
{
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<ILogger<RedisCartRepository>> _loggerMock;
    private readonly RedisCartRepository _repository;

    public RedisCartRepositoryTests()
    {
        _cacheMock = new Mock<IDistributedCache>();
        _loggerMock = new Mock<ILogger<RedisCartRepository>>();
        _repository = new RedisCartRepository(_cacheMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetCartAsync_WhenKeyExists_ShouldReturnDeserializedCart()
    {
        // Arrange
        var customerId = "cust-123";
        var originalCart = new CartAggregate(customerId);
        originalCart.AddItem("PROD-001", 3);

        var json = JsonSerializer.Serialize(originalCart, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var bytes = Encoding.UTF8.GetBytes(json);

        _cacheMock
            .Setup(c => c.GetAsync($"cart:{customerId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);

        // Act
        var result = await _repository.GetCartAsync(customerId);

        // Assert
        result.Should().NotBeNull();
        result!.CustomerId.Should().Be(customerId);
        result.Items.Should().HaveCount(1);
        result.Items.First().Sku.Should().Be("PROD-001");
        result.Items.First().Quantity.Should().Be(3);
    }

    [Fact]
    public async Task GetCartAsync_WhenKeyDoesNotExist_ShouldReturnEmptyCart()
    {
        // Arrange
        var customerId = "cust-404";
        _cacheMock
            .Setup(c => c.GetAsync($"cart:{customerId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await _repository.GetCartAsync(customerId);

        // Assert
        result.Should().NotBeNull();
        result!.CustomerId.Should().Be(customerId);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCartAsync_WhenRedisThrowsException_ShouldFallBackToEmptyCart()
    {
        // Arrange
        var customerId = "cust-down";
        _cacheMock
            .Setup(c => c.GetAsync($"cart:{customerId}", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis connection timed out"));

        // Act
        var result = await _repository.GetCartAsync(customerId);

        // Assert
        result.Should().NotBeNull();
        result!.CustomerId.Should().Be(customerId);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCartAsync_WhenCacheCorrupted_ShouldEvictAndReturnEmptyCart()
    {
        // Arrange
        var customerId = "cust-corrupt";
        var corruptBytes = Encoding.UTF8.GetBytes("{ broken invalid json");

        _cacheMock
            .Setup(c => c.GetAsync($"cart:{customerId}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(corruptBytes);

        // Act
        var result = await _repository.GetCartAsync(customerId);

        // Assert
        result.Should().NotBeNull();
        result!.CustomerId.Should().Be(customerId);
        result.Items.Should().BeEmpty();

        _cacheMock.Verify(c => c.RemoveAsync($"cart:{customerId}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveCartAsync_ShouldSaveWith14DaySlidingExpiration()
    {
        // Arrange
        var customerId = "cust-save";
        var cart = new CartAggregate(customerId);
        cart.AddItem("PROD-999", 5);

        DistributedCacheEntryOptions? capturedOptions = null;
        _cacheMock
            .Setup(c => c.SetAsync(
                $"cart:{customerId}",
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((_, _, opts, _) => capturedOptions = opts)
            .Returns(Task.CompletedTask);

        // Act
        var saved = await _repository.SaveCartAsync(cart);

        // Assert
        saved.Should().BeSameAs(cart);
        capturedOptions.Should().NotBeNull();
        capturedOptions!.SlidingExpiration.Should().Be(TimeSpan.FromDays(14));
    }

    [Fact]
    public async Task ClearCartAsync_ShouldRemoveKeyFromCache()
    {
        // Arrange
        var customerId = "cust-clear";
        _cacheMock
            .Setup(c => c.RemoveAsync($"cart:{customerId}", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _repository.ClearCartAsync(customerId);

        // Assert
        result.Should().BeTrue();
        _cacheMock.Verify(c => c.RemoveAsync($"cart:{customerId}", It.IsAny<CancellationToken>()), Times.Once);
    }
}

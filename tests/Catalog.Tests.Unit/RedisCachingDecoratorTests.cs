using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.BuildingBlocks.Messaging.Serialization;
using ShopSphere.Services.Catalog.Application.Common.Interfaces;
using ShopSphere.Services.Catalog.Application.Products.Dtos;
using ShopSphere.Services.Catalog.Application.Products.Queries.GetProductBySku;
using ShopSphere.Services.Catalog.Infrastructure.Caching;

namespace ShopSphere.Services.Catalog.Tests.Unit;

public class RedisCachingDecoratorTests
{
    private readonly Mock<IGetProductBySkuQueryHandler> _innerHandlerMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<ILogger<CachedGetProductBySkuQueryHandler>> _decoratorLoggerMock = new();
    private readonly Mock<IDistributedCache> _distributedCacheMock = new();
    private readonly Mock<ILogger<DistributedCacheService>> _cacheServiceLoggerMock = new();

    #region CachedGetProductBySkuQueryHandler Tests

    [Fact]
    public void CacheKey_ShouldFollowConvention()
    {
        // Act
        var key = CachedGetProductBySkuQueryHandler.GetCacheKey("kb-rgb-001");

        // Assert
        key.Should().Be("catalog:product:KB-RGB-001");
    }

    [Fact]
    public async Task HandleAsync_WhenCacheHit_ShouldReturnFromCacheWithoutInvokingInnerHandler()
    {
        // Arrange
        var cachedDto = new ProductDto(
            Guid.NewGuid(),
            "Cached Keyboard",
            "Desc",
            "KB-RGB-001",
            89.99m,
            "USD",
            "Active",
            1,
            DateTime.UtcNow,
            null);

        _cacheServiceMock
            .Setup(x => x.GetAsync<ProductDto>("catalog:product:KB-RGB-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedDto);

        var decorator = new CachedGetProductBySkuQueryHandler(
            _innerHandlerMock.Object,
            _cacheServiceMock.Object,
            _decoratorLoggerMock.Object);

        var query = new GetProductBySkuQuery("KB-RGB-001");

        // Act
        var result = await decorator.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(cachedDto);
        result.Value.Name.Should().Be("Cached Keyboard");

        // Ensure database inner handler was NOT invoked
        _innerHandlerMock.Verify(x => x.HandleAsync(It.IsAny<GetProductBySkuQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCacheMiss_ShouldInvokeInnerHandlerAndPopulateCacheWith30MinTTL()
    {
        // Arrange
        var dbDto = new ProductDto(
            Guid.NewGuid(),
            "DB Keyboard",
            "Desc",
            "KB-RGB-001",
            99.99m,
            "USD",
            "Active",
            1,
            DateTime.UtcNow,
            null);

        _cacheServiceMock
            .Setup(x => x.GetAsync<ProductDto>("catalog:product:KB-RGB-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductDto?)null);

        _innerHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetProductBySkuQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dbDto));

        var decorator = new CachedGetProductBySkuQueryHandler(
            _innerHandlerMock.Object,
            _cacheServiceMock.Object,
            _decoratorLoggerMock.Object);

        var query = new GetProductBySkuQuery("KB-RGB-001");

        // Act
        var result = await decorator.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(dbDto);

        // Verify inner handler was invoked
        _innerHandlerMock.Verify(x => x.HandleAsync(query, It.IsAny<CancellationToken>()), Times.Once);

        // Verify cache was populated with 30-minute sliding TTL
        _cacheServiceMock.Verify(x => x.SetAsync(
            "catalog:product:KB-RGB-001",
            dbDto,
            TimeSpan.FromMinutes(30),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region DistributedCacheService Resilient Failure Paths

    [Fact]
    public async Task GetAsync_WhenRedisThrowsConnectionException_ShouldLogWarningAndReturnNull()
    {
        // Arrange
        _distributedCacheMock
            .Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis server is down"));

        var service = new DistributedCacheService(
            _distributedCacheMock.Object,
            _cacheServiceLoggerMock.Object);

        // Act
        var result = await service.GetAsync<ProductDto>("catalog:product:KB-001");

        // Assert: must not throw, must return null (fallback to DB)
        result.Should().BeNull();

        _cacheServiceLoggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Redis cache unavailable")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAsync_WhenDataCorrupted_ShouldLogWarningEvictKeyAndReturnNull()
    {
        // Arrange
        var corruptedBytes = Encoding.UTF8.GetBytes("{ corrupted-non-json-data ???");
        _distributedCacheMock
            .Setup(x => x.GetAsync("catalog:product:KB-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(corruptedBytes);

        var service = new DistributedCacheService(
            _distributedCacheMock.Object,
            _cacheServiceLoggerMock.Object);

        // Act
        var result = await service.GetAsync<ProductDto>("catalog:product:KB-001");

        // Assert: returns null and evicts corrupted key
        result.Should().BeNull();

        _distributedCacheMock.Verify(
            x => x.RemoveAsync("catalog:product:KB-001", It.IsAny<CancellationToken>()),
            Times.Once);

        _cacheServiceLoggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Deserialization failure")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SetAsync_WhenRedisThrows_ShouldLogWarningAndNotThrow()
    {
        // Arrange
        _distributedCacheMock
            .Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis connection error"));

        var service = new DistributedCacheService(
            _distributedCacheMock.Object,
            _cacheServiceLoggerMock.Object);

        var dto = new ProductDto(
            Guid.NewGuid(), "Keyboard", "Desc", "KB-001", 99m, "USD", "Active", 1, DateTime.UtcNow, null);

        // Act & Assert
        var act = async () => await service.SetAsync("catalog:product:KB-001", dto);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RemoveAsync_WhenRedisThrows_ShouldLogWarningAndNotThrow()
    {
        // Arrange
        _distributedCacheMock
            .Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis timeout"));

        var service = new DistributedCacheService(
            _distributedCacheMock.Object,
            _cacheServiceLoggerMock.Object);

        // Act & Assert
        var act = async () => await service.RemoveAsync("catalog:product:KB-001");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetAsync_WhenValidDataCached_ShouldDeserializeAndReturnDto()
    {
        // Arrange
        var dto = new ProductDto(
            Guid.NewGuid(), "Keyboard", "Desc", "KB-001", 99.99m, "USD", "Active", 1, DateTime.UtcNow, null);
        var json = ShopSphereJsonDefaults.Serialize(dto);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        _distributedCacheMock
            .Setup(x => x.GetAsync("catalog:product:KB-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(jsonBytes);

        var service = new DistributedCacheService(
            _distributedCacheMock.Object,
            _cacheServiceLoggerMock.Object);

        // Act
        var result = await service.GetAsync<ProductDto>("catalog:product:KB-001");

        // Assert
        result.Should().NotBeNull();
        result!.Sku.Should().Be("KB-001");
        result.Price.Should().Be(99.99m);
    }

    #endregion
}

using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ShopSphere.BuildingBlocks.Messaging.Serialization;
using ShopSphere.Services.Catalog.Application.Common.Interfaces;

namespace ShopSphere.Services.Catalog.Infrastructure.Caching;

public class DistributedCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<DistributedCacheService> _logger;

    public DistributedCacheService(
        IDistributedCache cache,
        ILogger<DistributedCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        string? cachedJson;
        try
        {
            cachedJson = await _cache.GetStringAsync(key, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache unavailable when reading key '{Key}'. Falling back to database.", key);
            return null;
        }

        if (string.IsNullOrWhiteSpace(cachedJson))
        {
            return null;
        }

        try
        {
            return ShopSphereJsonDefaults.Deserialize<T>(cachedJson);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Deserialization failure for cache key '{Key}'. Evicting corrupted cache entry.", key);
            try
            {
                await _cache.RemoveAsync(key, ct);
            }
            catch (Exception removeEx)
            {
                _logger.LogWarning(removeEx, "Failed to evict corrupted cache key '{Key}'.", key);
            }

            return null;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? slidingExpiration = null,
        CancellationToken ct = default) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        try
        {
            var json = ShopSphereJsonDefaults.Serialize(value);
            var options = new DistributedCacheEntryOptions
            {
                SlidingExpiration = slidingExpiration ?? TimeSpan.FromMinutes(30)
            };

            await _cache.SetStringAsync(key, json, options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache unavailable when setting key '{Key}'. Continuing without caching.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            await _cache.RemoveAsync(key, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis cache unavailable when removing key '{Key}'. Continuing.", key);
        }
    }
}

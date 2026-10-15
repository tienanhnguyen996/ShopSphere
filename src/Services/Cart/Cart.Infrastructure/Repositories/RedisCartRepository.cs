using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ShopSphere.Services.Cart.Application.Common.Interfaces;
using ShopSphere.Services.Cart.Domain;

namespace ShopSphere.Services.Cart.Infrastructure.Repositories;

public class RedisCartRepository : ICartRepository
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCartRepository> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RedisCartRepository(IDistributedCache cache, ILogger<RedisCartRepository> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Cart.Domain.Cart?> GetCartAsync(string customerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return null;
        }

        var key = GetCacheKey(customerId);

        try
        {
            var data = await _cache.GetAsync(key, ct);
            if (data is null || data.Length == 0)
            {
                return new Cart.Domain.Cart(customerId);
            }

            var cart = JsonSerializer.Deserialize<Cart.Domain.Cart>(data, _jsonOptions);
            return cart ?? new Cart.Domain.Cart(customerId);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Corrupted cart cache data for key {CacheKey}. Evicting and returning empty cart.", key);
            try
            {
                await _cache.RemoveAsync(key, ct);
            }
            catch
            {
                // Ignore removal failure on read fallback
            }

            return new Cart.Domain.Cart(customerId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve cart from Redis for customer {CustomerId}. Falling back to empty cart.", customerId);
            return new Cart.Domain.Cart(customerId);
        }
    }

    public async Task<Cart.Domain.Cart> SaveCartAsync(Cart.Domain.Cart cart, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cart);

        var key = GetCacheKey(cart.CustomerId);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(cart, _jsonOptions);

        var entryOptions = new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromDays(14)
        };

        await _cache.SetAsync(key, bytes, entryOptions, ct);
        return cart;
    }

    public async Task<bool> ClearCartAsync(string customerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return false;
        }

        var key = GetCacheKey(customerId);
        await _cache.RemoveAsync(key, ct);
        return true;
    }

    private static string GetCacheKey(string customerId) => $"cart:{customerId.Trim()}";
}

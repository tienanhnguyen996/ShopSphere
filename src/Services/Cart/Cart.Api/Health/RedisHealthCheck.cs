using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ShopSphere.Services.Cart.Api.Health;

public class RedisHealthCheck : IHealthCheck
{
    private readonly IDistributedCache _cache;
    private const string HealthKey = "cart:healthcheck:ping";

    public RedisHealthCheck(IDistributedCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var pingBytes = "pong"u8.ToArray();
            await _cache.SetAsync(HealthKey, pingBytes, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
            }, cancellationToken);

            var readBack = await _cache.GetAsync(HealthKey, cancellationToken);
            if (readBack is null || readBack.Length == 0)
            {
                return HealthCheckResult.Degraded("Redis read back empty payload during health check.");
            }

            return HealthCheckResult.Healthy("Redis distributed cache is responsive.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Redis distributed cache is unavailable: {ex.Message}", ex);
        }
    }
}

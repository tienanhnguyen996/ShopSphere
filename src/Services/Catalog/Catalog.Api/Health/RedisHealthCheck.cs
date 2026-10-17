using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ShopSphere.Services.Catalog.Api.Health;

public class RedisHealthCheck : IHealthCheck
{
    private readonly IDistributedCache _cache;

    public RedisHealthCheck(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var testKey = "__catalog_health_ping__";
            await _cache.SetStringAsync(testKey, "healthy", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5)
            }, cancellationToken);

            var value = await _cache.GetStringAsync(testKey, cancellationToken);
            return value == "healthy"
                ? HealthCheckResult.Healthy("Redis cache is responsive.")
                : HealthCheckResult.Unhealthy("Redis cache ping returned invalid response.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis cache is unavailable.", ex);
        }
    }
}

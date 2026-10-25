using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShopSphere.Services.Cart.Application.Common.Interfaces;

namespace ShopSphere.Services.Cart.Api.Health;

public class CatalogServiceHealthCheck : IHealthCheck
{
    private readonly ICatalogClient _catalogClient;

    public CatalogServiceHealthCheck(ICatalogClient catalogClient)
    {
        _catalogClient = catalogClient ?? throw new ArgumentNullException(nameof(catalogClient));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            // Health ping query
            var result = await _catalogClient.GetProductBySkuAsync("HEALTH-CHECK-PING", cts.Token);
            if (result.IsFailure && result.Error.Code == "Cart.CatalogUnavailable")
            {
                return HealthCheckResult.Unhealthy($"Catalog service downstream dependency is unavailable: {result.Error.Description}");
            }

            return HealthCheckResult.Healthy("Catalog service dependency is responsive.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Failed to contact Catalog service: {ex.Message}", ex);
        }
    }
}

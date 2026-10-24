using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShopSphere.Services.Order.Infrastructure;

namespace ShopSphere.Services.Order.Api.Health;

public class SqlServerHealthCheck : IHealthCheck
{
    private readonly OrderDbContext _dbContext;

    public SqlServerHealthCheck(OrderDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("SQL Server database connection is responsive.")
                : HealthCheckResult.Unhealthy("SQL Server database connection failed.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server database is unavailable.", ex);
        }
    }
}

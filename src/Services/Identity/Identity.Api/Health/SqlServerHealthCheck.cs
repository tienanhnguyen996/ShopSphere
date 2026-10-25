using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShopSphere.Services.Identity.Infrastructure;

namespace ShopSphere.Services.Identity.Api.Health;

public class SqlServerHealthCheck : IHealthCheck
{
    private readonly IdentityDbContext _dbContext;

    public SqlServerHealthCheck(IdentityDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var canConnect = await _dbContext.Database.CanConnectAsync(cts.Token);
            return canConnect
                ? HealthCheckResult.Healthy("SQL Server database connection is responsive.")
                : HealthCheckResult.Degraded("SQL Server database connection failed.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("SQL Server database is unavailable.", ex);
        }
    }
}

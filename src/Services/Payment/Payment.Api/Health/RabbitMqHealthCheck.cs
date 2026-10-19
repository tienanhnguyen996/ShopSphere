using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ShopSphere.Services.Payment.Api.Health;

public class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IBusControl? _busControl;

    public RabbitMqHealthCheck(IServiceProvider serviceProvider)
    {
        _busControl = serviceProvider.GetService<IBusControl>();
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_busControl is null)
            {
                return Task.FromResult(HealthCheckResult.Healthy("RabbitMQ bus registered."));
            }

            var endpoint = _busControl.CheckHealth();
            return Task.FromResult(endpoint.Status == BusHealthStatus.Healthy
                ? HealthCheckResult.Healthy("RabbitMQ bus is active and connected.")
                : HealthCheckResult.Unhealthy($"RabbitMQ bus status is {endpoint.Status}."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ bus is unavailable.", ex));
        }
    }
}

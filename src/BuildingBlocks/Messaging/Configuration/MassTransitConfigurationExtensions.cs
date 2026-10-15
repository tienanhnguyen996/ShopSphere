using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ShopSphere.BuildingBlocks.Messaging.Configuration;

public static class MassTransitConfigurationExtensions
{
    public static IServiceCollection AddShopSphereMessaging(
        this IServiceCollection services,
        IConfiguration config,
        Action<IBusRegistrationConfigurator>? configure = null)
    {
        services.AddMassTransit(busConfig =>
        {
            busConfig.SetKebabCaseEndpointNameFormatter();

            configure?.Invoke(busConfig);

            busConfig.UsingRabbitMq((context, cfg) =>
            {
                var host = config["RabbitMQ:Host"] ?? "localhost";
                var portString = config["RabbitMQ:Port"];
                var port = ushort.TryParse(portString, out var p) ? p : (ushort)5672;
                var username = config["RabbitMQ:Username"] ?? "guest";
                var password = config["RabbitMQ:Password"] ?? "guest";

                cfg.Host(host, port, "/", h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                cfg.UseMessageRetry(r =>
                {
                    r.Intervals(
                        TimeSpan.FromMilliseconds(200),
                        TimeSpan.FromMilliseconds(500),
                        TimeSpan.FromSeconds(1),
                        TimeSpan.FromSeconds(2),
                        TimeSpan.FromSeconds(5));
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}

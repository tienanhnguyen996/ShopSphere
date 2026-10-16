using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.BuildingBlocks.Messaging.Configuration;
using Xunit;

namespace ShopSphere.BuildingBlocks.Tests.Unit;

public class MassTransitConfigurationTests
{
    [Fact]
    public void AddShopSphereMessaging_RegistersBusControlAndFormatter()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:Host"] = "localhost",
                ["RabbitMQ:Port"] = "5672",
                ["RabbitMQ:Username"] = "guest",
                ["RabbitMQ:Password"] = "guest"
            })
            .Build();

        services.AddShopSphereMessaging(configuration);

        var serviceProvider = services.BuildServiceProvider();
        var busControl = serviceProvider.GetService<IBusControl>();
        var formatter = serviceProvider.GetService<IEndpointNameFormatter>();

        busControl.Should().NotBeNull();
        formatter.Should().NotBeNull();
        formatter.Should().BeOfType<KebabCaseEndpointNameFormatter>();
    }
}

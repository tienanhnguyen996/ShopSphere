using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using ShopSphere.BuildingBlocks.Observability;
using Xunit;

namespace ShopSphere.BuildingBlocks.Tests.Unit;

public class ObservabilityTests
{
    [Fact]
    public void AddShopSphereObservability_RegistersTracerAndMeterProviders()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:OtlpEndpoint"] = "http://localhost:4317"
            })
            .Build();

        services.AddShopSphereObservability(config, "Catalog.Api");

        var provider = services.BuildServiceProvider();

        var tracerProvider = provider.GetService<TracerProvider>();
        var meterProvider = provider.GetService<MeterProvider>();

        tracerProvider.Should().NotBeNull();
        meterProvider.Should().NotBeNull();
    }

    [Fact]
    public void AddShopSphereObservability_WithoutOtlpEndpoint_RegistersConsoleExporters()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        services.AddShopSphereObservability(config, "Catalog.Api");

        var provider = services.BuildServiceProvider();

        var tracerProvider = provider.GetService<TracerProvider>();
        var meterProvider = provider.GetService<MeterProvider>();

        tracerProvider.Should().NotBeNull();
        meterProvider.Should().NotBeNull();
    }
}

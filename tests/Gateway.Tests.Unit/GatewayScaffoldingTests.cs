using FluentAssertions;
using Xunit;

namespace ShopSphere.Gateway.Tests.Unit;

public class GatewayScaffoldingTests
{
    [Fact]
    public void GatewayAssembly_ShouldBeAvailable()
    {
        var assembly = typeof(Program).Assembly;
        assembly.Should().NotBeNull();
    }
}

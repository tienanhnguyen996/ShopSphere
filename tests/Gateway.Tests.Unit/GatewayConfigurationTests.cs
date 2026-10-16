using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using ShopSphere.Gateway.Middleware;
using Xunit;
using Yarp.ReverseProxy.Forwarder;

namespace ShopSphere.Gateway.Tests.Unit;

public class GatewayConfigurationTests
{
    private static IConfiguration LoadGatewayConfiguration()
    {
        var appsettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(appsettingsPath))
        {
            // Fallback to project directory if not copied to output yet
            var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../src/Gateway/ShopSphere.Gateway"));
            appsettingsPath = Path.Combine(projectDir, "appsettings.json");
        }

        File.Exists(appsettingsPath).Should().BeTrue($"appsettings.json must exist at {appsettingsPath}");

        return new ConfigurationBuilder()
            .AddJsonFile(appsettingsPath, optional: false, reloadOnChange: false)
            .Build();
    }

    [Fact]
    public void ReverseProxyConfiguration_ShouldContainAllMicroserviceRoutes()
    {
        var configuration = LoadGatewayConfiguration();

        var routes = configuration.GetSection("ReverseProxy:Routes");
        routes.Exists().Should().BeTrue("ReverseProxy:Routes section must exist");

        // Catalog route
        routes["catalog-route:Match:Path"].Should().Be("/api/v1/catalog/{**catch-all}");
        routes["catalog-route:ClusterId"].Should().Be("catalog-cluster");

        // Catalog products route
        routes["catalog-products-route:Match:Path"].Should().Be("/api/v1/products/{**catch-all}");
        routes["catalog-products-route:ClusterId"].Should().Be("catalog-cluster");

        // Cart route
        routes["cart-route:Match:Path"].Should().Be("/api/v1/cart/{**catch-all}");
        routes["cart-route:ClusterId"].Should().Be("cart-cluster");

        // Inventory route
        routes["inventory-route:Match:Path"].Should().Be("/api/v1/inventory/{**catch-all}");
        routes["inventory-route:ClusterId"].Should().Be("inventory-cluster");

        // Payment route
        routes["payment-route:Match:Path"].Should().Be("/api/v1/payment/{**catch-all}");
        routes["payment-route:ClusterId"].Should().Be("payment-cluster");

        // Orders route
        routes["orders-route:Match:Path"].Should().Be("/api/v1/orders/{**catch-all}");
        routes["orders-route:ClusterId"].Should().Be("order-cluster");

        // Shipping route
        routes["shipping-route:Match:Path"].Should().Be("/api/v1/shipping/{**catch-all}");
        routes["shipping-route:ClusterId"].Should().Be("shipping-cluster");
    }

    [Fact]
    public void ReverseProxyConfiguration_ShouldContainAllMicroserviceClusters()
    {
        var configuration = LoadGatewayConfiguration();

        var clusters = configuration.GetSection("ReverseProxy:Clusters");
        clusters.Exists().Should().BeTrue("ReverseProxy:Clusters section must exist");

        var expectedClusters = new Dictionary<string, (string Destination, string HealthPath, string Interval)>
        {
            ["catalog-cluster"] = ("http://localhost:5001", "/health", "00:00:15"),
            ["cart-cluster"] = ("http://localhost:5002", "/health", "00:00:15"),
            ["inventory-cluster"] = ("http://localhost:5003", "/health", "00:00:15"),
            ["payment-cluster"] = ("http://localhost:5004", "/health", "00:00:15"),
            ["order-cluster"] = ("http://localhost:5005", "/health", "00:00:15"),
            ["shipping-cluster"] = ("http://localhost:5006", "/health", "00:00:15")
        };

        foreach (var (clusterId, (expectedDestination, expectedHealthPath, expectedInterval)) in expectedClusters)
        {
            var clusterSection = clusters.GetSection(clusterId);
            clusterSection.Exists().Should().BeTrue($"Cluster {clusterId} should exist in configuration");

            var destinations = clusterSection.GetSection("Destinations").GetChildren().ToList();
            destinations.Should().NotBeEmpty($"Cluster {clusterId} must have at least one destination configured");
            destinations.First()["Address"].Should().Be(expectedDestination);

            clusterSection["HealthCheck:Active:Path"].Should().Be(expectedHealthPath);
            clusterSection["HealthCheck:Active:Interval"].Should().Be(expectedInterval);
        }
    }

    [Fact]
    public async Task GatewayErrorMiddleware_WhenForwarderErrorPresent_ShouldReturn503Json()
    {
        var context = new DefaultHttpContext();
        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;
        context.TraceIdentifier = "test-trace-id-12345";

        var errorFeature = new TestForwarderErrorFeature(ForwarderError.NoAvailableDestinations);
        context.Features.Set<IForwarderErrorFeature>(errorFeature);

        var middleware = new GatewayErrorHandlingMiddleware(nextContext => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.ContentType.Should().StartWith("application/json");

        responseStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(responseStream);
        var responseBody = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        root.GetProperty("type").GetString().Should().Be("https://tools.ietf.org/html/rfc7231#section-6.6.4");
        root.GetProperty("title").GetString().Should().Be("Service Unavailable");
        root.GetProperty("status").GetInt32().Should().Be(503);
        root.GetProperty("detail").GetString().Should().Be("Downstream service is temporarily unavailable or unreachable.");
        root.GetProperty("code").GetString().Should().Be("Gateway.ServiceUnavailable");
        root.GetProperty("traceId").GetString().Should().Be("test-trace-id-12345");
    }

    [Fact]
    public async Task GatewayErrorMiddleware_WhenNoForwarderError_ShouldPassThroughUnchanged()
    {
        var context = new DefaultHttpContext();
        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var middleware = new GatewayErrorHandlingMiddleware(async nextContext =>
        {
            nextContext.Response.StatusCode = StatusCodes.Status200OK;
            await nextContext.Response.WriteAsync("Success payload");
        });

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        responseStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(responseStream);
        var body = await reader.ReadToEndAsync();
        body.Should().Be("Success payload");
    }

    [Fact]
    public async Task GatewayErrorMiddleware_WhenForwarderErrorIsNone_ShouldPassThroughUnchanged()
    {
        var context = new DefaultHttpContext();
        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        var errorFeature = new TestForwarderErrorFeature(ForwarderError.None);
        context.Features.Set<IForwarderErrorFeature>(errorFeature);

        var middleware = new GatewayErrorHandlingMiddleware(async nextContext =>
        {
            nextContext.Response.StatusCode = StatusCodes.Status200OK;
            await nextContext.Response.WriteAsync("Healthy passthrough");
        });

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        responseStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(responseStream);
        var body = await reader.ReadToEndAsync();
        body.Should().Be("Healthy passthrough");
    }

    [Theory]
    [InlineData(ForwarderError.RequestTimedOut)]
    [InlineData(ForwarderError.NoAvailableDestinations)]
    public async Task GatewayErrorMiddleware_WhenDownstreamErrorOccurs_ShouldReturn503Json(ForwarderError error)
    {
        var context = new DefaultHttpContext();
        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;
        context.TraceIdentifier = "trace-timeout-123";

        var errorFeature = new TestForwarderErrorFeature(error);
        context.Features.Set<IForwarderErrorFeature>(errorFeature);

        var middleware = new GatewayErrorHandlingMiddleware(nextContext => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.ContentType.Should().StartWith("application/json");

        responseStream.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(responseStream);
        var responseBody = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(responseBody);
        doc.RootElement.GetProperty("code").GetString().Should().Be("Gateway.ServiceUnavailable");
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(503);
    }

    [Fact]
    public async Task GatewayErrorMiddleware_WhenResponseHasStarted_ShouldNotModifyResponse()
    {
        var context = new DefaultHttpContext();
        var responseFeature = new TestHttpResponseFeature { HasStarted = true, StatusCode = StatusCodes.Status200OK };
        context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>(responseFeature);

        var errorFeature = new TestForwarderErrorFeature(ForwarderError.NoAvailableDestinations);
        context.Features.Set<IForwarderErrorFeature>(errorFeature);

        var middleware = new GatewayErrorHandlingMiddleware(nextContext => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    private sealed class TestHttpResponseFeature : Microsoft.AspNetCore.Http.Features.IHttpResponseFeature
    {
        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted { get; set; }
        public void OnStarting(Func<object, Task> callback, object state) { }
        public void OnCompleted(Func<object, Task> callback, object state) { }
    }

    private sealed class TestForwarderErrorFeature : IForwarderErrorFeature
    {
        public TestForwarderErrorFeature(ForwarderError error, Exception? exception = null)
        {
            Error = error;
            Exception = exception;
        }

        public ForwarderError Error { get; }
        public Exception? Exception { get; }
    }
}

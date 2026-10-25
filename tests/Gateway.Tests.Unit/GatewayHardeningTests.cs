using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using ShopSphere.Gateway.Middleware;
using Xunit;

namespace ShopSphere.Gateway.Tests.Unit;

public class GatewayHardeningTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GatewayHardeningTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Gateway_ShouldPropagateCorrelationId_AndSetSecurityAndTraceHeaders()
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(GatewayTraceAndSecurityHeadersMiddleware.CorrelationIdHeader, "corr-test-999");
        request.Headers.Add(GatewayTraceAndSecurityHeadersMiddleware.TraceParentHeader, "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues(GatewayTraceAndSecurityHeadersMiddleware.CorrelationIdHeader)
            .Should().Contain("corr-test-999");
        response.Headers.Contains(GatewayTraceAndSecurityHeadersMiddleware.TraceIdHeader).Should().BeTrue();
        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Contain("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().Contain("strict-origin-when-cross-origin");
        response.Headers.GetValues("X-Permitted-Cross-Domain-Policies").Should().Contain("none");
        response.Headers.GetValues("Content-Security-Policy").Should().Contain("default-src 'none'; frame-ancestors 'none'");
    }

    [Fact]
    public async Task Gateway_WhenCorrelationIdHeaderIsMissing_ShouldGenerateAndReturnCorrelationId()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues(GatewayTraceAndSecurityHeadersMiddleware.CorrelationIdHeader, out var values)
            .Should().BeTrue();
        values!.First().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Gateway_RateLimiting_WhenClientExceedsAuthPermitLimit_Returns429WithProblemDetailsAndRetryAfter()
    {
        await using var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:AuthPermitLimit"] = "2",
                    ["RateLimiting:GlobalPermitLimit"] = "5",
                    ["RateLimiting:WindowSeconds"] = "60"
                });
            });
        });

        using var client = customFactory.CreateClient();
        var clientIp = "203.0.113.42";

        // First 2 requests are permitted by the rate limiter (downstream is not running in unit test so returns 503)
        for (var i = 0; i < 2; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login");
            req.Headers.Add("X-Forwarded-For", clientIp);
            var resp = await client.SendAsync(req);
            resp.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        // 3rd request from the same client IP exceeds AuthPermitLimit (2) and must be rejected with 429
        using var throttledReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login");
        throttledReq.Headers.Add("X-Forwarded-For", clientIp);
        var throttledResp = await client.SendAsync(throttledReq);

        throttledResp.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        throttledResp.Headers.RetryAfter.Should().NotBeNull();

        var body = await throttledResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("status").GetInt32().Should().Be(429);
        doc.RootElement.GetProperty("code").GetString().Should().Be("Gateway.RateLimitExceeded");
        doc.RootElement.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }
}

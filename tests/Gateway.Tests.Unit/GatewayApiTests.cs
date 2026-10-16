using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace ShopSphere.Gateway.Tests.Unit;

public class GatewayApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public GatewayApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Gateway_HealthEndpoint_ShouldReturn200Healthy()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        jsonDoc.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Theory]
    [InlineData("http://localhost:3000")]
    [InlineData("http://localhost:5173")]
    public async Task Gateway_CorsRequest_ShouldIncludeCorsHeadersForAllowedOrigins(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", origin);

        var response = await _client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeTrue();
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain(origin);
        response.Headers.Contains("Access-Control-Allow-Credentials").Should().BeTrue();
        response.Headers.GetValues("Access-Control-Allow-Credentials").Should().Contain("true");
    }

    [Fact]
    public async Task Gateway_CorsRequest_ShouldNotIncludeCorsHeadersForDisallowedOrigin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "http://untrusted-site.com");

        var response = await _client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Gateway_UnmatchedRoute_ShouldReturn404NotFound()
    {
        var response = await _client.GetAsync("/api/v1/nonexistent");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Gateway_ConfiguredRouteWithUnreachableDownstream_ShouldReturn503ServiceUnavailableWithProblemDetails()
    {
        var response = await _client.GetAsync("/api/v1/catalog/items");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        var root = jsonDoc.RootElement;

        root.GetProperty("status").GetInt32().Should().Be(503);
        root.GetProperty("code").GetString().Should().Be("Gateway.ServiceUnavailable");
        root.GetProperty("title").GetString().Should().Be("Service Unavailable");
        root.GetProperty("detail").GetString().Should().Be("Downstream service is temporarily unavailable or unreachable.");
        root.TryGetProperty("traceId", out var traceIdProp).Should().BeTrue();
        traceIdProp.GetString().Should().NotBeNullOrWhiteSpace();
    }
}

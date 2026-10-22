using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ShopSphere.BuildingBlocks.Security;
using Xunit;
using Yarp.ReverseProxy.Forwarder;

namespace ShopSphere.Gateway.Tests.Unit;

public class GatewayAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public GatewayAuthTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string GenerateJwt(
        string[]? permissions = null,
        string subject = "gateway-test-user")
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(AuthenticationExtensions.DefaultDevKey);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subject),
            new("sub", subject)
        };

        if (permissions != null)
        {
            foreach (var permission in permissions)
            {
                claims.Add(new Claim("permission", permission));
            }
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(1),
            Issuer = AuthenticationExtensions.DefaultIssuer,
            Audience = AuthenticationExtensions.DefaultAudience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    [Fact]
    public async Task Gateway_AuthenticationAndAuthorizationServices_AreConfigured()
    {
        using var scope = _factory.Services.CreateScope();
        var authSchemeProvider = scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>();
        var defaultScheme = await authSchemeProvider.GetDefaultAuthenticateSchemeAsync();

        defaultScheme.Should().NotBeNull();
        defaultScheme!.Name.Should().Be(JwtBearerDefaults.AuthenticationScheme);

        var policyProvider = scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>();
        var createProductPolicy = await policyProvider.GetPolicyAsync(ShopSpherePermissions.Catalog.CreateProduct);
        createProductPolicy.Should().NotBeNull();
    }

    [Fact]
    public async Task Gateway_HealthEndpoint_RemainsAnonymousAndHealthy()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Gateway_HealthEndpoint_WithValidBearerToken_Succeeds()
    {
        var token = GenerateJwt([ShopSpherePermissions.SuperAdmin]);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Gateway_ForwardsAuthorizationHeaderDownstream()
    {
        var capturedRequests = new List<HttpRequestMessage>();
        var testHandler = new CapturingHttpMessageHandler(capturedRequests);

        var customFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IForwarderHttpClientFactory>(new TestForwarderHttpClientFactory(testHandler));
            });
        });

        var client = customFactory.CreateClient();

        var token = GenerateJwt([ShopSpherePermissions.Catalog.CreateProduct]);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/catalog/items");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-Correlation-Id", "corr-test-123");

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var forwardedRequest = capturedRequests.FirstOrDefault(r => r.RequestUri != null && r.RequestUri.PathAndQuery.Contains("items"));
        forwardedRequest.Should().NotBeNull("Expected proxy request for /items to be forwarded");
        forwardedRequest!.Headers.Authorization.Should().NotBeNull();
        forwardedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        forwardedRequest.Headers.Authorization!.Parameter.Should().Be(token);
        forwardedRequest.Headers.Contains("X-Correlation-Id").Should().BeTrue();
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly List<HttpRequestMessage> _capturedRequests;

        public CapturingHttpMessageHandler(List<HttpRequestMessage> capturedRequests)
        {
            _capturedRequests = capturedRequests;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _capturedRequests.Add(request);

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"ok\"}", Encoding.UTF8, "application/json")
            };

            return Task.FromResult(response);
        }
    }

    private sealed class TestForwarderHttpClientFactory : IForwarderHttpClientFactory
    {
        private readonly HttpMessageInvoker _invoker;

        public TestForwarderHttpClientFactory(HttpMessageHandler handler)
        {
            _invoker = new HttpMessageInvoker(handler);
        }

        public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) => _invoker;
    }
}

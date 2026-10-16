using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using ShopSphere.BuildingBlocks.Security;
using ShopSphere.Services.Payment.Api;
using ShopSphere.Services.Payment.Application.Common.Interfaces;
using ShopSphere.Services.Payment.Domain;
using ShopSphere.Services.Payment.Tests.Unit.Helpers;
using Xunit;

namespace ShopSphere.Services.Payment.Tests.Unit;

public class PaymentAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly List<PaymentAttempt> _paymentAttempts = new();

    public PaymentAuthTests(WebApplicationFactory<Program> factory)
    {
        var mockDbContext = new Mock<IPaymentDbContext>();
        mockDbContext.Setup(d => d.PaymentAttempts).Returns(() => MockDbSetExtensions.CreateMockDbSet(_paymentAttempts).Object);
        mockDbContext.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPaymentDbContext>();
                services.AddScoped<IPaymentDbContext>(_ => mockDbContext.Object);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task GetPaymentAttempt_WithoutToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;
        var attemptId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/payment/attempts/{attemptId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPaymentAttempt_WithTokenLackingPermission_ShouldReturn403Forbidden()
    {
        // Arrange
        var token = GenerateJwtToken(ShopSpherePermissions.Payment.Create); // Lacks payment:read
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var attemptId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/payment/attempts/{attemptId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPaymentAttempt_WithValidReadPermission_ShouldPassAuthorization()
    {
        // Arrange
        var token = GenerateJwtToken(ShopSpherePermissions.Payment.Read);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var attemptId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/payment/attempts/{attemptId}");

        // Assert: Authorization passes (not 401 and not 403)
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostPaymentAttempt_WithoutToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;
        var request = new CreatePaymentAttemptRequest(Guid.NewGuid(), 99.95m, "USD", "credit_card");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/payment/attempts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostPaymentAttempt_WithTokenLackingPermission_ShouldReturn403Forbidden()
    {
        // Arrange
        var token = GenerateJwtToken(ShopSpherePermissions.Payment.Read); // Lacks payment:create
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new CreatePaymentAttemptRequest(Guid.NewGuid(), 99.95m, "USD", "credit_card");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/payment/attempts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostPaymentAttempt_WithValidCreatePermission_ShouldSucceed()
    {
        // Arrange
        var token = GenerateJwtToken(ShopSpherePermissions.Payment.Create);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new CreatePaymentAttemptRequest(Guid.NewGuid(), 99.95m, "USD", "credit_card");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/payment/attempts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Health_WithoutToken_ShouldRemainAnonymous()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    private static string GenerateJwtToken(params string[] permissions)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthenticationExtensions.DefaultDevKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, "payment-service@shopsphere.local")
        };

        foreach (var permission in permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        var token = new JwtSecurityToken(
            issuer: AuthenticationExtensions.DefaultIssuer,
            audience: AuthenticationExtensions.DefaultAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

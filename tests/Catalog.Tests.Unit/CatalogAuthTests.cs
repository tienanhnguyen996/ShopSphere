using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using ShopSphere.BuildingBlocks.Security;
using ShopSphere.Services.Catalog.Api;
using ShopSphere.Services.Catalog.Application.Common.Interfaces;
using ShopSphere.Services.Catalog.Domain;
using ShopSphere.Services.Catalog.Tests.Unit.Helpers;
using Xunit;

namespace ShopSphere.Services.Catalog.Tests.Unit;

public class CatalogAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly List<Product> _products = new();

    public CatalogAuthTests(WebApplicationFactory<Program> factory)
    {
        var mockDbContext = new Mock<ICatalogDbContext>();
        mockDbContext.Setup(c => c.Products).Returns(() => MockDbSetExtensions.CreateMockDbSet(_products).Object);
        mockDbContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var mockCache = new Mock<IDistributedCache>();

        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ICatalogDbContext>();
                services.AddScoped<ICatalogDbContext>(_ => mockDbContext.Object);

                services.RemoveAll<IDistributedCache>();
                services.AddSingleton<IDistributedCache>(_ => mockCache.Object);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task PostProduct_WithoutToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        var request = new CreateProductRequest("Mouse", "Optical mouse", "MOUSE-01", 25.00m, "USD");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/catalog/products", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostProduct_WithTokenLackingPermission_ShouldReturn403Forbidden()
    {
        // Arrange
        var token = GenerateJwtToken(ShopSpherePermissions.CatalogRead); // Only read permission
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new CreateProductRequest("Mouse", "Optical mouse", "MOUSE-01", 25.00m, "USD");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/catalog/products", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostProduct_WithValidCreatePermission_ShouldSucceed()
    {
        // Arrange
        var token = GenerateJwtToken(ShopSpherePermissions.Catalog.CreateProduct);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new CreateProductRequest("Mouse", "Optical mouse", "MOUSE-01", 25.00m, "USD");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/catalog/products", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task PutProductPrice_WithoutToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        var request = new UpdatePriceRequest(29.99m, "USD");

        // Act
        var response = await _client.PutAsJsonAsync("/api/v1/catalog/products/MOUSE-01/price", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PutProductPrice_WithTokenLackingPermission_ShouldReturn403Forbidden()
    {
        // Arrange
        var token = GenerateJwtToken(ShopSpherePermissions.CatalogRead);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new UpdatePriceRequest(29.99m, "USD");

        // Act
        var response = await _client.PutAsJsonAsync("/api/v1/catalog/products/MOUSE-01/price", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutProductPrice_WithValidPriceUpdatePermission_ShouldNotBeUnauthorizedOrForbidden()
    {
        // Arrange
        var token = GenerateJwtToken(ShopSpherePermissions.Catalog.UpdatePrice);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new UpdatePriceRequest(29.99m, "USD");

        // Act
        var response = await _client.PutAsJsonAsync("/api/v1/catalog/products/MOUSE-01/price", request);

        // Assert: not 401 and not 403 (could be 404 because MOUSE-01 does not exist in mock, but authorization passed)
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetProducts_WithoutToken_ShouldRemainAnonymousAndAccessible()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync("/api/v1/catalog/products");

        // Assert
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
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
            new(JwtRegisteredClaimNames.Email, "catalog-admin@shopsphere.local")
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

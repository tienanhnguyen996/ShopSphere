using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ShopSphere.BuildingBlocks.Security;

namespace ShopSphere.Services.Catalog.Tests.Integration;

[Trait("Category", "Integration")]
public class ProductApiTests : IClassFixture<CatalogWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ProductApiTests(CatalogWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        var token = GenerateJwtToken(ShopSpherePermissions.Catalog.CreateProduct, ShopSpherePermissions.Catalog.UpdatePrice);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task HealthCheck_ShouldReturnOk()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateProduct_AndGetProducts_ShouldPersistAndReturnProduct()
    {
        // Arrange
        var sku = $"TEST-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var createRequest = new CreateProductRequest(
            Name: "Test Gaming Mouse",
            Description: "High-precision sensor mouse",
            Sku: sku,
            PriceAmount: 59.99m,
            Currency: "USD",
            ActivateImmediately: true);

        // Act - Create
        var createResponse = await _client.PostAsJsonAsync("/api/v1/products", createRequest);

        // Assert - Create
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<ProductResponse>();
        created.Should().NotBeNull();
        created!.Name.Should().Be(createRequest.Name);
        created.Sku.Should().Be(createRequest.Sku);
        created.Price.Should().Be(59.99m);
        created.PriceVersion.Should().Be(1);

        // Act - Get By Id
        var getResponse = await _client.GetAsync($"/api/v1/products/{created.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Update Price
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/v1/products/{created.Id}/price",
            new UpdatePriceRequest(69.99m, "USD"));

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ProductResponse>();
        updated.Should().NotBeNull();
        updated!.Price.Should().Be(69.99m);
        updated.PriceVersion.Should().Be(2);
    }

    [Fact]
    public async Task CreateProduct_WithDuplicateSku_ShouldReturnConflict()
    {
        // Arrange
        var sku = $"DUP-{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        var request = new CreateProductRequest("Duplicate Item 1", "Desc", sku, 10m, "USD");

        // Act
        var firstResponse = await _client.PostAsJsonAsync("/api/v1/products", request);
        var secondResponse = await _client.PostAsJsonAsync("/api/v1/products", request);

        // Assert
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
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

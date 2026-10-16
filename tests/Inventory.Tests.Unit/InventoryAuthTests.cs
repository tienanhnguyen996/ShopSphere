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
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.BuildingBlocks.Security;
using ShopSphere.Services.Inventory.Api;
using ShopSphere.Services.Inventory.Application.Stock.Commands.AdjustStock;
using ShopSphere.Services.Inventory.Application.Stock.Dtos;
using ShopSphere.Services.Inventory.Application.Stock.Queries.GetStockBySku;
using Xunit;

namespace ShopSphere.Services.Inventory.Tests.Unit;

public class InventoryAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public InventoryAuthTests(WebApplicationFactory<Program> factory)
    {
        var mockAdjustHandler = new Mock<IAdjustStockCommandHandler>();
        mockAdjustHandler.Setup(h => h.HandleAsync(It.IsAny<AdjustStockCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new StockDto(Guid.NewGuid(), "SKU-01", 100, 0, 0, 100, DateTime.UtcNow)));

        var mockGetSkuHandler = new Mock<IGetStockBySkuQueryHandler>();
        mockGetSkuHandler.Setup(h => h.HandleAsync(It.IsAny<GetStockBySkuQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new StockDto(Guid.NewGuid(), "SKU-01", 100, 0, 0, 100, DateTime.UtcNow)));

        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAdjustStockCommandHandler>();
                services.AddScoped<IAdjustStockCommandHandler>(_ => mockAdjustHandler.Object);

                services.RemoveAll<IGetStockBySkuQueryHandler>();
                services.AddScoped<IGetStockBySkuQueryHandler>(_ => mockGetSkuHandler.Object);
            });
        }).CreateClient();
    }

    [Fact]
    public async Task PostAdjustments_WithoutToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;
        var request = new StockAdjustmentRequest("SKU-01", 10, "Manual intake");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/inventory/adjustments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostAdjustments_WithTokenLackingPermission_ShouldReturn403Forbidden()
    {
        // Arrange
        var token = GenerateJwtToken(ShopSpherePermissions.InventoryRead); // Missing stock:adjust
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new StockAdjustmentRequest("SKU-01", 10, "Manual intake");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/inventory/adjustments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostAdjustments_WithValidAdjustPermission_ShouldSucceed()
    {
        // Arrange
        var token = GenerateJwtToken(ShopSpherePermissions.Inventory.AdjustStock);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var request = new StockAdjustmentRequest("SKU-01", 10, "Manual intake");

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/inventory/adjustments", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetStockBySku_WithoutToken_ShouldRemainAnonymousAndAccessible()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _client.GetAsync("/api/v1/inventory/SKU-01");

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
            new(JwtRegisteredClaimNames.Email, "inventory-admin@shopsphere.local")
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

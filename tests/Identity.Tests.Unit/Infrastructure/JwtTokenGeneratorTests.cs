using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ShopSphere.BuildingBlocks.Security;
using ShopSphere.Services.Identity.Domain;
using ShopSphere.Services.Identity.Infrastructure.Services;
using Xunit;

namespace ShopSphere.Services.Identity.Tests.Unit.Infrastructure;

public class JwtTokenGeneratorTests
{
    private readonly IConfiguration _configuration;
    private readonly JwtTokenGenerator _tokenGenerator;

    public JwtTokenGeneratorTests()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "SuperSecretKeyForIdentityTestsWithAtLeast32Bytes!",
                ["Jwt:Issuer"] = "ShopSphere",
                ["Jwt:Audience"] = "ShopSphereApi",
                ["Jwt:ExpiryMinutes"] = "15"
            })
            .Build();

        _tokenGenerator = new JwtTokenGenerator(_configuration);
    }

    [Fact]
    public void GenerateToken_WithStandardUser_ShouldProduceValidSignedJwt_WithExpectedClaims()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "john@example.com", "hash123", "John Doe");
        var roles = new[] { "Customer" };
        var permissions = new[] { ShopSpherePermissions.CartManage, ShopSpherePermissions.CatalogRead };

        // Act
        var tokenString = _tokenGenerator.GenerateToken(user, roles, permissions);

        // Assert
        tokenString.Should().NotBeNullOrWhiteSpace();

        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(tokenString).Should().BeTrue();

        var jwtToken = handler.ReadJwtToken(tokenString);
        jwtToken.Issuer.Should().Be("ShopSphere");
        jwtToken.Audiences.Should().Contain("ShopSphereApi");

        var claims = jwtToken.Claims.ToList();
        claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email);
        claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Name && c.Value == user.FullName);
        claims.Should().Contain(c => c.Type == "role" && c.Value == "Customer");
        claims.Should().Contain(c => c.Type == "permission" && c.Value == ShopSpherePermissions.CartManage);
        claims.Should().Contain(c => c.Type == "permission" && c.Value == ShopSpherePermissions.CatalogRead);

        jwtToken.ValidTo.Should().BeAfter(DateTime.UtcNow.AddMinutes(10));
    }

    [Fact]
    public void GenerateToken_WithSuperAdminWildcard_ShouldExpandToAllConcretePermissions()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "admin@shopsphere.local", "hash123", "Admin User");
        var roles = new[] { "Admin" };
        var permissions = new[] { ShopSpherePermissions.SuperAdmin }; // "*:*"

        // Act
        var tokenString = _tokenGenerator.GenerateToken(user, roles, permissions);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);
        var permClaims = jwtToken.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();

        // Wildcard should expand to ALL concrete permissions
        permClaims.Should().Contain(ShopSpherePermissions.CatalogRead);
        permClaims.Should().Contain(ShopSpherePermissions.CatalogCreate);
        permClaims.Should().Contain(ShopSpherePermissions.CatalogUpdate);
        permClaims.Should().Contain(ShopSpherePermissions.CatalogPriceUpdate);
        permClaims.Should().Contain(ShopSpherePermissions.InventoryRead);
        permClaims.Should().Contain(ShopSpherePermissions.InventoryAdjust);
        permClaims.Should().Contain(ShopSpherePermissions.CartManage);
        permClaims.Should().Contain(ShopSpherePermissions.PaymentRead);
        permClaims.Should().Contain(ShopSpherePermissions.PaymentRefund);
        permClaims.Should().Contain(ShopSpherePermissions.UsersRead);
        permClaims.Should().Contain(ShopSpherePermissions.UsersManage);
        permClaims.Should().Contain(ShopSpherePermissions.SuperAdmin);
    }

    [Fact]
    public void GenerateToken_WithPrefixWildcard_ShouldExpandToMatchingServicePermissions()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "catalog-mgr@example.com", "hash123", "Catalog Manager");
        var roles = new[] { "CatalogManager" };
        var permissions = new[] { "catalog:*" };

        // Act
        var tokenString = _tokenGenerator.GenerateToken(user, roles, permissions);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);
        var permClaims = jwtToken.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();

        permClaims.Should().Contain(ShopSpherePermissions.CatalogRead);
        permClaims.Should().Contain(ShopSpherePermissions.CatalogCreate);
        permClaims.Should().Contain(ShopSpherePermissions.CatalogUpdate);
        permClaims.Should().Contain(ShopSpherePermissions.CatalogPriceUpdate);
        permClaims.Should().NotContain(ShopSpherePermissions.PaymentRefund);
        permClaims.Should().NotContain(ShopSpherePermissions.InventoryAdjust);
    }

    [Fact]
    public void GenerateToken_WithDuplicates_ShouldDeduplicateRoleAndPermissionClaims()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "user@example.com", "hash123", "User");
        var roles = new[] { "Customer", "Customer", "Customer" };
        var permissions = new[] { ShopSpherePermissions.CartManage, ShopSpherePermissions.CartManage };

        // Act
        var tokenString = _tokenGenerator.GenerateToken(user, roles, permissions);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(tokenString);

        jwtToken.Claims.Where(c => c.Type == "role" && c.Value == "Customer").Should().HaveCount(1);
        jwtToken.Claims.Where(c => c.Type == "permission" && c.Value == ShopSpherePermissions.CartManage).Should().HaveCount(1);
    }

    [Fact]
    public void GenerateToken_CanBeValidatedWithSymmetricSecurityKey()
    {
        // Arrange
        var secret = "SuperSecretKeyForIdentityTestsWithAtLeast32Bytes!";
        var user = User.Create(Guid.NewGuid(), "valid@example.com", "hash123", "Valid User");
        var tokenString = _tokenGenerator.GenerateToken(user, new[] { "Customer" }, new[] { "cart:manage" });

        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "ShopSphere",
            ValidateAudience = true,
            ValidAudience = "ShopSphereApi",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // Act
        var principal = handler.ValidateToken(tokenString, validationParameters, out var validatedToken);

        // Assert
        principal.Should().NotBeNull();
        validatedToken.Should().NotBeNull();
        principal.FindFirst(ClaimTypes.NameIdentifier)?.Value.Should().Be(user.Id.ToString());
    }
}

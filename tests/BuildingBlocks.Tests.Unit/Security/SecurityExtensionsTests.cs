using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShopSphere.BuildingBlocks.Security;
using Xunit;

namespace ShopSphere.BuildingBlocks.Tests.Unit.Security;

public class SecurityExtensionsTests
{
    [Fact]
    public void ShopSpherePermissions_DefinesExpectedConstants()
    {
        ShopSpherePermissions.CatalogRead.Should().Be("catalog:products:read");
        ShopSpherePermissions.CatalogCreate.Should().Be("catalog:products:create");
        ShopSpherePermissions.CatalogPriceUpdate.Should().Be("catalog:prices:update");
        ShopSpherePermissions.InventoryAdjust.Should().Be("inventory:stock:adjust");
        ShopSpherePermissions.PaymentRead.Should().Be("payment:read");
        ShopSpherePermissions.CartManage.Should().Be("cart:manage");
        ShopSpherePermissions.UsersManage.Should().Be("users:manage");
        ShopSpherePermissions.UsersRead.Should().Be("users:read");
        ShopSpherePermissions.SuperAdmin.Should().Be("*:*");

        ShopSpherePermissions.All.Should().Contain(new[]
        {
            ShopSpherePermissions.CatalogRead,
            ShopSpherePermissions.CatalogCreate,
            ShopSpherePermissions.CatalogPriceUpdate,
            ShopSpherePermissions.InventoryAdjust,
            ShopSpherePermissions.PaymentRead,
            ShopSpherePermissions.CartManage,
            ShopSpherePermissions.UsersManage,
            ShopSpherePermissions.UsersRead,
            ShopSpherePermissions.SuperAdmin
        });
    }

    [Fact]
    public async Task AddShopSphereAuthentication_ConfiguresJwtBearerWithCustomSettings()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "SuperSecretSigningKeyThatIsLongEnoughForHmacSha256!",
                ["Jwt:Issuer"] = "CustomIssuer",
                ["Jwt:Audience"] = "CustomAudience"
            })
            .Build();

        services.AddShopSphereAuthentication(config);

        var provider = services.BuildServiceProvider();
        var schemeProvider = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var defaultScheme = await schemeProvider.GetDefaultAuthenticateSchemeAsync();

        defaultScheme.Should().NotBeNull();
        defaultScheme!.Name.Should().Be(JwtBearerDefaults.AuthenticationScheme);

        var optionsSnapshot = provider.GetRequiredService<IOptionsSnapshot<JwtBearerOptions>>();
        var jwtOptions = optionsSnapshot.Get(JwtBearerDefaults.AuthenticationScheme);

        jwtOptions.TokenValidationParameters.ValidateIssuer.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidIssuer.Should().Be("CustomIssuer");
        jwtOptions.TokenValidationParameters.ValidateAudience.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidAudience.Should().Be("CustomAudience");
        jwtOptions.TokenValidationParameters.ValidateIssuerSigningKey.Should().BeTrue();
        jwtOptions.TokenValidationParameters.IssuerSigningKey.Should().NotBeNull();
        jwtOptions.TokenValidationParameters.ValidateLifetime.Should().BeTrue();
    }

    [Fact]
    public void AddShopSphereAuthentication_WithDefaultSettings_AppliesDefaults()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        services.AddShopSphereAuthentication(config);

        var provider = services.BuildServiceProvider();
        var optionsSnapshot = provider.GetRequiredService<IOptionsSnapshot<JwtBearerOptions>>();
        var jwtOptions = optionsSnapshot.Get(JwtBearerDefaults.AuthenticationScheme);

        jwtOptions.TokenValidationParameters.ValidIssuer.Should().Be(AuthenticationExtensions.DefaultIssuer);
        jwtOptions.TokenValidationParameters.ValidAudience.Should().Be(AuthenticationExtensions.DefaultAudience);
        jwtOptions.TokenValidationParameters.IssuerSigningKey.Should().NotBeNull();
    }

    [Fact]
    public async Task AddShopSphereAuthorization_RegistersPolicies_AndEnforcesPermissions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddShopSphereAuthorization();

        var provider = services.BuildServiceProvider();
        var authService = provider.GetRequiredService<IAuthorizationService>();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Verify policy exists for CatalogCreate
        var catalogCreatePolicy = await policyProvider.GetPolicyAsync(ShopSpherePermissions.CatalogCreate);
        catalogCreatePolicy.Should().NotBeNull();

        // User with permission succeeds
        var authorizedUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("permission", ShopSpherePermissions.CatalogCreate)
        }, "Bearer"));

        var successResult = await authService.AuthorizeAsync(authorizedUser, null, ShopSpherePermissions.CatalogCreate);
        successResult.Succeeded.Should().BeTrue();

        // User without permission fails
        var unauthorizedUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("permission", ShopSpherePermissions.CatalogRead)
        }, "Bearer"));

        var failureResult = await authService.AuthorizeAsync(unauthorizedUser, null, ShopSpherePermissions.CatalogCreate);
        failureResult.Succeeded.Should().BeFalse();

        // User with wildcard SuperAdmin succeeds
        var superAdminUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("permission", ShopSpherePermissions.SuperAdmin)
        }, "Bearer"));

        var superAdminResult = await authService.AuthorizeAsync(superAdminUser, null, ShopSpherePermissions.CatalogCreate);
        superAdminResult.Succeeded.Should().BeTrue();
    }
}

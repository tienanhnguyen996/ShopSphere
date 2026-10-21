using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using ShopSphere.BuildingBlocks.Security;
using ShopSphere.Services.Identity.Application.Common.Interfaces;
using ShopSphere.Services.Identity.Domain;
using ShopSphere.Services.Identity.Infrastructure;
using ShopSphere.Services.Identity.Infrastructure.Services;
using ShopSphere.Services.Identity.Tests.Unit.Helpers;
using Xunit;

namespace ShopSphere.Services.Identity.Tests.Unit.Infrastructure;

public class IdentityDbSeederTests
{
    private readonly Mock<IIdentityDbContext> _mockContext;
    private readonly List<User> _users = new();
    private readonly List<Role> _roles = new();
    private readonly List<Permission> _permissions = new();
    private readonly List<UserRole> _userRoles = new();
    private readonly List<RolePermission> _rolePermissions = new();
    private readonly PasswordHashService _passwordHashService = new();

    public IdentityDbSeederTests()
    {
        _mockContext = new Mock<IIdentityDbContext>();
        _mockContext.Setup(c => c.Users).Returns(() => MockDbSetExtensions.CreateMockDbSet(_users).Object);
        _mockContext.Setup(c => c.Roles).Returns(() => MockDbSetExtensions.CreateMockDbSet(_roles).Object);
        _mockContext.Setup(c => c.Permissions).Returns(() => MockDbSetExtensions.CreateMockDbSet(_permissions).Object);
        _mockContext.Setup(c => c.UserRoles).Returns(() => MockDbSetExtensions.CreateMockDbSet(_userRoles).Object);
        _mockContext.Setup(c => c.RolePermissions).Returns(() => MockDbSetExtensions.CreateMockDbSet(_rolePermissions).Object);
        _mockContext.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task SeedAsync_OnEmptyDatabase_ShouldSeedPermissionsRolesAndDefaultAdmin()
    {
        // Act
        await IdentityDbSeeder.SeedAsync(_mockContext.Object, _passwordHashService);

        // Assert Permissions
        _permissions.Should().HaveCount(ShopSpherePermissions.All.Count);
        _permissions.Select(p => p.Code).Should().Contain(ShopSpherePermissions.All);

        // Assert Roles
        _roles.Should().HaveCount(2);
        var adminRole = _roles.FirstOrDefault(r => r.Name == "Admin");
        adminRole.Should().NotBeNull();
        adminRole!.RolePermissions.Should().HaveCount(ShopSpherePermissions.All.Count);

        var customerRole = _roles.FirstOrDefault(r => r.Name == "Customer");
        customerRole.Should().NotBeNull();
        var customerPermIds = customerRole!.RolePermissions.Select(rp => rp.PermissionId).ToList();
        var customerPermCodes = _permissions.Where(p => customerPermIds.Contains(p.Id)).Select(p => p.Code).ToList();
        customerPermCodes.Should().Contain(new[] { ShopSpherePermissions.CartManage, ShopSpherePermissions.CatalogRead });

        // Assert Default Admin User
        _users.Should().HaveCount(1);
        var adminUser = _users.First();
        adminUser.Email.Should().Be("admin@shopsphere.local");
        adminUser.FullName.Should().Be(IdentityDbSeeder.DefaultAdminFullName);
        adminUser.IsActive.Should().BeTrue();
        adminUser.UserRoles.Should().Contain(ur => ur.RoleId == adminRole.Id);

        // Verify password hash
        _passwordHashService.VerifyPassword("AdminPassword123!", adminUser.PasswordHash).Should().BeTrue();

        _mockContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SeedAsync_WhenRunTwice_ShouldBeIdempotentAndNotDuplicateData()
    {
        // Arrange - initial seed
        await IdentityDbSeeder.SeedAsync(_mockContext.Object, _passwordHashService);
        var initialUserCount = _users.Count;
        var initialRoleCount = _roles.Count;
        var initialPermCount = _permissions.Count;

        // Act - second seed
        await IdentityDbSeeder.SeedAsync(_mockContext.Object, _passwordHashService);

        // Assert - counts remain identical
        _users.Should().HaveCount(initialUserCount);
        _roles.Should().HaveCount(initialRoleCount);
        _permissions.Should().HaveCount(initialPermCount);
    }

    [Fact]
    public async Task SeedAsync_WithCustomConfiguration_ShouldUseConfiguredAdminCredentials()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:DefaultAdmin:Email"] = "superadmin@customdomain.io",
                ["Identity:DefaultAdmin:Password"] = "SuperSecretCustomPassword999!"
            })
            .Build();

        // Act
        await IdentityDbSeeder.SeedAsync(_mockContext.Object, _passwordHashService, configuration);

        // Assert
        var adminUser = _users.FirstOrDefault(u => u.Email == "superadmin@customdomain.io");
        adminUser.Should().NotBeNull();
        _passwordHashService.VerifyPassword("SuperSecretCustomPassword999!", adminUser!.PasswordHash).Should().BeTrue();
    }
}

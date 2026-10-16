using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.Services.Identity.Domain;
using ShopSphere.Services.Identity.Infrastructure;
using Xunit;

namespace ShopSphere.Services.Identity.Tests.Unit.Infrastructure;

public class IdentityDbContextTests
{
    private readonly IdentityDbContext _context;

    public IdentityDbContextTests()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=localhost;Database=ShopSphereIdentityTest;User Id=sa;Password=Your_password123;TrustServerCertificate=True;")
            .Options;

        _context = new IdentityDbContext(options);
    }

    [Fact]
    public void Model_ShouldUseIdentityDefaultSchema()
    {
        _context.Model.GetDefaultSchema().Should().Be("identity");
    }

    [Fact]
    public void UserEntity_ShouldBeMappedWithUniqueEmail()
    {
        var entityType = _context.Model.FindEntityType(typeof(User))!;
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("Users");
        entityType.GetSchema().Should().Be("identity");

        var emailProp = entityType.FindProperty(nameof(User.Email))!;
        emailProp.Should().NotBeNull();
        emailProp.IsNullable.Should().BeFalse();

        var index = entityType.FindIndex(emailProp);
        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void RoleEntity_ShouldBeMappedWithUniqueName()
    {
        var entityType = _context.Model.FindEntityType(typeof(Role))!;
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("Roles");
        entityType.GetSchema().Should().Be("identity");

        var nameProp = entityType.FindProperty(nameof(Role.Name))!;
        nameProp.Should().NotBeNull();
        nameProp.IsNullable.Should().BeFalse();

        var index = entityType.FindIndex(nameProp);
        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void PermissionEntity_ShouldBeMappedWithUniqueCode()
    {
        var entityType = _context.Model.FindEntityType(typeof(Permission))!;
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("Permissions");
        entityType.GetSchema().Should().Be("identity");

        var codeProp = entityType.FindProperty(nameof(Permission.Code))!;
        codeProp.Should().NotBeNull();
        codeProp.IsNullable.Should().BeFalse();

        var index = entityType.FindIndex(codeProp);
        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void UserRoleEntity_ShouldHaveCompositePrimaryKey()
    {
        var entityType = _context.Model.FindEntityType(typeof(UserRole))!;
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("UserRoles");

        var pk = entityType.FindPrimaryKey();
        pk.Should().NotBeNull();
        pk!.Properties.Select(p => p.Name).Should().Contain(new[] { nameof(UserRole.UserId), nameof(UserRole.RoleId) });
    }

    [Fact]
    public void RolePermissionEntity_ShouldHaveCompositePrimaryKey()
    {
        var entityType = _context.Model.FindEntityType(typeof(RolePermission))!;
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("RolePermissions");

        var pk = entityType.FindPrimaryKey();
        pk.Should().NotBeNull();
        pk!.Properties.Select(p => p.Name).Should().Contain(new[] { nameof(RolePermission.RoleId), nameof(RolePermission.PermissionId) });
    }

    [Fact]
    public void RefreshTokenEntity_ShouldBeMappedWithIndexesAndIgnoredCalculatedProperties()
    {
        var entityType = _context.Model.FindEntityType(typeof(RefreshToken))!;
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("RefreshTokens");

        var tokenHashProp = entityType.FindProperty(nameof(RefreshToken.TokenHash))!;
        tokenHashProp.Should().NotBeNull();

        var hashIndex = entityType.FindIndex(tokenHashProp);
        hashIndex.Should().NotBeNull();

        // Calculated properties should not be mapped
        entityType.FindProperty(nameof(RefreshToken.IsActive)).Should().BeNull();
        entityType.FindProperty(nameof(RefreshToken.IsExpired)).Should().BeNull();
        entityType.FindProperty(nameof(RefreshToken.IsRevoked)).Should().BeNull();
    }

    [Fact]
    public void OutboxMessageEntity_ShouldBeMappedToOutboxMessagesTable()
    {
        var entityType = _context.Model.FindEntityType(typeof(OutboxMessage))!;
        entityType.Should().NotBeNull();
        entityType.GetTableName().Should().Be("OutboxMessages");
        entityType.GetSchema().Should().Be("identity");
    }

    [Fact]
    public void Factory_ShouldCreateValidDbContext()
    {
        var factory = new IdentityDbContextFactory();
        var context = factory.CreateDbContext(Array.Empty<string>());
        context.Should().NotBeNull();
        context.Model.GetDefaultSchema().Should().Be("identity");
    }
}

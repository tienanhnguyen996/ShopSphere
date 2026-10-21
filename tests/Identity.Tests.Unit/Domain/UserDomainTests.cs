using FluentAssertions;
using ShopSphere.Services.Identity.Domain;
using ShopSphere.Services.Identity.Domain.Exceptions;
using Xunit;

namespace ShopSphere.Services.Identity.Tests.Unit.Domain;

public class UserDomainTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateActiveUserWithNormalizedEmail()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "  Test.User@Example.COM  ";
        var passwordHash = "hashed_secret_value";
        var fullName = "  Test User  ";

        // Act
        var user = User.Create(userId, email, passwordHash, fullName);

        // Assert
        user.Should().NotBeNull();
        user.Id.Should().Be(userId);
        user.Email.Should().Be("test.user@example.com");
        user.PasswordHash.Should().Be(passwordHash);
        user.FullName.Should().Be("Test User");
        user.IsActive.Should().BeTrue();
        user.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        user.UpdatedAtUtc.Should().BeNull();
        user.UserRoles.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithEmptyId_ShouldThrowIdentityDomainException()
    {
        // Act
        var act = () => User.Create(Guid.Empty, "user@example.com", "hash", "User Name");

        // Assert
        act.Should().Throw<IdentityDomainException>()
            .WithMessage("*identifier*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyEmail_ShouldThrowIdentityDomainException(string? invalidEmail)
    {
        // Act
        var act = () => User.Create(Guid.NewGuid(), invalidEmail!, "hash", "User Name");

        // Assert
        act.Should().Throw<IdentityDomainException>()
            .WithMessage("*email*");
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("invalid.email.com")]
    [InlineData("@missingusername.com")]
    public void Create_WithInvalidEmailFormat_ShouldThrowIdentityDomainException(string invalidEmail)
    {
        // Act
        var act = () => User.Create(Guid.NewGuid(), invalidEmail, "hash", "User Name");

        // Assert
        act.Should().Throw<IdentityDomainException>()
            .WithMessage("*email*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyPasswordHash_ShouldThrowIdentityDomainException(string? invalidHash)
    {
        // Act
        var act = () => User.Create(Guid.NewGuid(), "user@example.com", invalidHash!, "User Name");

        // Assert
        act.Should().Throw<IdentityDomainException>()
            .WithMessage("*password*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyFullName_ShouldThrowIdentityDomainException(string? invalidName)
    {
        // Act
        var act = () => User.Create(Guid.NewGuid(), "user@example.com", "hash", invalidName!);

        // Assert
        act.Should().Throw<IdentityDomainException>()
            .WithMessage("*name*");
    }

    [Fact]
    public void AssignRole_WithValidRoleId_ShouldAddUserRoleAndSetUpdatedAt()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "user@example.com", "hash", "User Name");
        var roleId = Guid.NewGuid();

        // Act
        user.AssignRole(roleId);

        // Assert
        user.UserRoles.Should().HaveCount(1);
        user.UserRoles.First().RoleId.Should().Be(roleId);
        user.UserRoles.First().UserId.Should().Be(user.Id);
        user.UpdatedAtUtc.Should().NotBeNull();
        user.UpdatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void AssignRole_WithEmptyRoleId_ShouldThrowIdentityDomainException()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "user@example.com", "hash", "User Name");

        // Act
        var act = () => user.AssignRole(Guid.Empty);

        // Assert
        act.Should().Throw<IdentityDomainException>()
            .WithMessage("*role*");
    }

    [Fact]
    public void AssignRole_WhenRoleAlreadyAssigned_ShouldBeIdempotent()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "user@example.com", "hash", "User Name");
        var roleId = Guid.NewGuid();
        user.AssignRole(roleId);

        // Act
        user.AssignRole(roleId);

        // Assert
        user.UserRoles.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveRole_WithExistingRoleId_ShouldRemoveRoleAndSetUpdatedAt()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "user@example.com", "hash", "User Name");
        var roleId1 = Guid.NewGuid();
        var roleId2 = Guid.NewGuid();
        user.AssignRole(roleId1);
        user.AssignRole(roleId2);

        // Act
        user.RemoveRole(roleId1);

        // Assert
        user.UserRoles.Should().HaveCount(1);
        user.UserRoles.First().RoleId.Should().Be(roleId2);
        user.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void RemoveRole_WithEmptyRoleId_ShouldThrowIdentityDomainException()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "user@example.com", "hash", "User Name");

        // Act
        var act = () => user.RemoveRole(Guid.Empty);

        // Assert
        act.Should().Throw<IdentityDomainException>()
            .WithMessage("*role*");
    }

    [Fact]
    public void RemoveRole_WithNonExistingRoleId_ShouldNotThrow()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "user@example.com", "hash", "User Name");
        var roleId = Guid.NewGuid();

        // Act
        var act = () => user.RemoveRole(roleId);

        // Assert
        act.Should().NotThrow();
        user.UserRoles.Should().BeEmpty();
    }

    [Fact]
    public void Role_Create_WithValidParameters_ShouldCreateRole()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var name = "Admin";
        var description = "System Administrator";

        // Act
        var role = Role.Create(roleId, name, description);

        // Assert
        role.Should().NotBeNull();
        role.Id.Should().Be(roleId);
        role.Name.Should().Be("Admin");
        role.Description.Should().Be("System Administrator");
        role.RolePermissions.Should().BeEmpty();
    }

    [Fact]
    public void Role_Create_WithEmptyId_ShouldThrowIdentityDomainException()
    {
        // Act
        var act = () => Role.Create(Guid.Empty, "Admin", "Desc");

        // Assert
        act.Should().Throw<IdentityDomainException>()
            .WithMessage("*identifier*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Role_Create_WithEmptyName_ShouldThrowIdentityDomainException(string? invalidName)
    {
        // Act
        var act = () => Role.Create(Guid.NewGuid(), invalidName!, "Desc");

        // Assert
        act.Should().Throw<IdentityDomainException>()
            .WithMessage("*name*");
    }

    [Fact]
    public void Role_AddPermission_ShouldAddPermissionAndBeIdempotent()
    {
        // Arrange
        var role = Role.Create(Guid.NewGuid(), "Admin", "Desc");
        var permissionId = Guid.NewGuid();

        // Act
        role.AddPermission(permissionId);
        role.AddPermission(permissionId);

        // Assert
        role.RolePermissions.Should().HaveCount(1);
        role.RolePermissions.First().PermissionId.Should().Be(permissionId);
        role.RolePermissions.First().RoleId.Should().Be(role.Id);
    }

    [Fact]
    public void Role_AddPermission_WithEmptyPermissionId_ShouldThrowIdentityDomainException()
    {
        // Arrange
        var role = Role.Create(Guid.NewGuid(), "Admin", "Desc");

        // Act
        var act = () => role.AddPermission(Guid.Empty);

        // Assert
        act.Should().Throw<IdentityDomainException>()
            .WithMessage("*permission*");
    }

    [Fact]
    public void Role_RemovePermission_ShouldRemovePermission()
    {
        // Arrange
        var role = Role.Create(Guid.NewGuid(), "Admin", "Desc");
        var permissionId = Guid.NewGuid();
        role.AddPermission(permissionId);

        // Act
        role.RemovePermission(permissionId);

        // Assert
        role.RolePermissions.Should().BeEmpty();
    }

    [Fact]
    public void Permission_Create_WithValidParameters_ShouldCreatePermission()
    {
        // Arrange
        var permissionId = Guid.NewGuid();
        var code = "catalog:products:read";
        var description = "Read products";

        // Act
        var permission = Permission.Create(permissionId, code, description);

        // Assert
        permission.Should().NotBeNull();
        permission.Id.Should().Be(permissionId);
        permission.Code.Should().Be("catalog:products:read");
        permission.Description.Should().Be("Read products");
    }

    [Fact]
    public void Permission_Create_WithEmptyCode_ShouldThrowIdentityDomainException()
    {
        // Act
        var act = () => Permission.Create(Guid.NewGuid(), "   ", "Desc");

        // Assert
        act.Should().Throw<IdentityDomainException>()
            .WithMessage("*code*");
    }
}

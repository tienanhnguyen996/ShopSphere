using FluentAssertions;
using Moq;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.Services.Identity.Application.Auth.Commands.Login;
using ShopSphere.Services.Identity.Application.Auth.Commands.RefreshToken;
using ShopSphere.Services.Identity.Application.Auth.Commands.Register;
using ShopSphere.Services.Identity.Application.Auth.Commands.RevokeToken;
using ShopSphere.Services.Identity.Application.Auth.Queries.GetCurrentUser;
using ShopSphere.Services.Identity.Application.Common.Interfaces;
using ShopSphere.Services.Identity.Application.Common.Security;
using ShopSphere.Services.Identity.Application.Roles.Queries.GetRoles;
using ShopSphere.Services.Identity.Application.Users.Commands.AssignRole;
using ShopSphere.Services.Identity.Application.Users.Commands.RemoveRole;
using ShopSphere.Services.Identity.Application.Users.Queries.GetUsers;
using ShopSphere.Services.Identity.Domain;
using ShopSphere.Services.Identity.Tests.Unit.Helpers;
using Xunit;

namespace ShopSphere.Services.Identity.Tests.Unit.Application;

public class AuthCommandTests
{
    private readonly Mock<IIdentityDbContext> _dbContextMock;
    private readonly Mock<IJwtTokenGenerator> _tokenGeneratorMock;
    private readonly Mock<IPasswordHashService> _passwordHashMock;

    private readonly List<User> _users;
    private readonly List<Role> _roles;
    private readonly List<Permission> _permissions;
    private readonly List<UserRole> _userRoles;
    private readonly List<RolePermission> _rolePermissions;
    private readonly List<RefreshToken> _refreshTokens;
    private readonly List<OutboxMessage> _outboxMessages;

    public AuthCommandTests()
    {
        _dbContextMock = new Mock<IIdentityDbContext>();
        _tokenGeneratorMock = new Mock<IJwtTokenGenerator>();
        _passwordHashMock = new Mock<IPasswordHashService>();

        _users = new List<User>();
        _roles = new List<Role>();
        _permissions = new List<Permission>();
        _userRoles = new List<UserRole>();
        _rolePermissions = new List<RolePermission>();
        _refreshTokens = new List<RefreshToken>();
        _outboxMessages = new List<OutboxMessage>();

        var usersMock = MockDbSetExtensions.CreateMockDbSet(_users);
        var rolesMock = MockDbSetExtensions.CreateMockDbSet(_roles);
        var permissionsMock = MockDbSetExtensions.CreateMockDbSet(_permissions);
        var userRolesMock = MockDbSetExtensions.CreateMockDbSet(_userRoles);
        var rolePermissionsMock = MockDbSetExtensions.CreateMockDbSet(_rolePermissions);
        var refreshTokensMock = MockDbSetExtensions.CreateMockDbSet(_refreshTokens);
        var outboxMessagesMock = MockDbSetExtensions.CreateMockDbSet(_outboxMessages);

        _dbContextMock.Setup(x => x.Users).Returns(usersMock.Object);
        _dbContextMock.Setup(x => x.Roles).Returns(rolesMock.Object);
        _dbContextMock.Setup(x => x.Permissions).Returns(permissionsMock.Object);
        _dbContextMock.Setup(x => x.UserRoles).Returns(userRolesMock.Object);
        _dbContextMock.Setup(x => x.RolePermissions).Returns(rolePermissionsMock.Object);
        _dbContextMock.Setup(x => x.RefreshTokens).Returns(refreshTokensMock.Object);
        _dbContextMock.Setup(x => x.OutboxMessages).Returns(outboxMessagesMock.Object);
        _dbContextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    #region Register Tests

    [Fact]
    public async Task Register_WithValidCommand_ShouldCreateUserAndReturnTokenResponse()
    {
        // Arrange
        var customerRole = Role.Create(Guid.NewGuid(), "Customer", "Customer Role");
        _roles.Add(customerRole);

        _passwordHashMock.Setup(x => x.HashPassword("Password123!"))
            .Returns("hashed_password_123");

        _tokenGeneratorMock.Setup(x => x.GenerateToken(
                It.IsAny<User>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IReadOnlyCollection<string>>()))
            .Returns("mocked-jwt-access-token");

        var validator = new RegisterCommandValidator();
        var handler = new RegisterCommandHandler(
            _dbContextMock.Object,
            _tokenGeneratorMock.Object,
            _passwordHashMock.Object,
            validator);

        var command = new RegisterCommand("newuser@shopsphere.local", "Password123!", "Jane Doe");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("mocked-jwt-access-token");
        result.Value.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.Value.TokenType.Should().Be("Bearer");
        result.Value.ExpiresAtUtc.Should().BeAfter(DateTime.UtcNow);

        _users.Should().ContainSingle(u => u.Email == "newuser@shopsphere.local");
        var createdUser = _users.First();
        createdUser.FullName.Should().Be("Jane Doe");
        createdUser.PasswordHash.Should().Be("hashed_password_123");
        createdUser.UserRoles.Should().ContainSingle(ur => ur.RoleId == customerRole.Id);

        _refreshTokens.Should().ContainSingle(rt => rt.UserId == createdUser.Id);
        _outboxMessages.Should().ContainSingle(m => m.Type.Contains("UserRegistered"));
    }

    [Fact]
    public async Task Register_WithInvalidEmailOrPassword_ShouldFailValidation()
    {
        // Arrange
        var validator = new RegisterCommandValidator();
        var handler = new RegisterCommandHandler(
            _dbContextMock.Object,
            _tokenGeneratorMock.Object,
            _passwordHashMock.Object,
            validator);

        var command = new RegisterCommand("not-an-email", "123", "");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("User.ValidationFailed");
    }

    [Fact]
    public async Task Register_WithExistingEmail_ShouldReturnConflict()
    {
        // Arrange
        var existingUser = User.Create(Guid.NewGuid(), "existing@shopsphere.local", "hash", "Existing User");
        _users.Add(existingUser);

        var validator = new RegisterCommandValidator();
        var handler = new RegisterCommandHandler(
            _dbContextMock.Object,
            _tokenGeneratorMock.Object,
            _passwordHashMock.Object,
            validator);

        var command = new RegisterCommand("existing@shopsphere.local", "Password123!", "Another Name");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("User.DuplicateEmail");
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnTokenResponse()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "user@shopsphere.local", "hash", "Active User");
        var role = Role.Create(Guid.NewGuid(), "Customer", "Customer Role");
        var perm = Permission.Create(Guid.NewGuid(), "cart:manage", "Manage cart");
        role.AddPermission(perm.Id);
        user.AssignRole(role.Id);

        _users.Add(user);
        _roles.Add(role);
        _permissions.Add(perm);
        _userRoles.Add(new UserRole(user.Id, role.Id));
        _rolePermissions.Add(new RolePermission(role.Id, perm.Id));

        _passwordHashMock.Setup(x => x.VerifyPassword("Password123!", "hash"))
            .Returns(true);

        _tokenGeneratorMock.Setup(x => x.GenerateToken(
                It.Is<User>(u => u.Id == user.Id),
                It.Is<IReadOnlyCollection<string>>(r => r.Contains("Customer")),
                It.Is<IReadOnlyCollection<string>>(p => p.Contains("cart:manage"))))
            .Returns("access-token-xyz");

        var validator = new LoginCommandValidator();
        var handler = new LoginCommandHandler(
            _dbContextMock.Object,
            _tokenGeneratorMock.Object,
            _passwordHashMock.Object,
            validator);

        var command = new LoginCommand("user@shopsphere.local", "Password123!");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token-xyz");
        result.Value.RefreshToken.Should().NotBeNullOrWhiteSpace();
        _refreshTokens.Should().ContainSingle(rt => rt.UserId == user.Id);
    }

    [Fact]
    public async Task Login_WithNonExistentEmail_ShouldReturnUnauthorizedWithoutLeakingExistence()
    {
        // Arrange
        var validator = new LoginCommandValidator();
        var handler = new LoginCommandHandler(
            _dbContextMock.Object,
            _tokenGeneratorMock.Object,
            _passwordHashMock.Object,
            validator);

        var command = new LoginCommand("nonexistent@shopsphere.local", "Password123!");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Login_WithIncorrectPassword_ShouldReturnUnauthorized()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "user@shopsphere.local", "hash", "Test User");
        _users.Add(user);

        _passwordHashMock.Setup(x => x.VerifyPassword("WrongPassword", "hash"))
            .Returns(false);

        var validator = new LoginCommandValidator();
        var handler = new LoginCommandHandler(
            _dbContextMock.Object,
            _tokenGeneratorMock.Object,
            _passwordHashMock.Object,
            validator);

        var command = new LoginCommand("user@shopsphere.local", "WrongPassword");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Login_WithInactiveUser_ShouldReturnUnauthorized()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "user@shopsphere.local", "hash", "Inactive User");
        user.Deactivate();
        _users.Add(user);

        _passwordHashMock.Setup(x => x.VerifyPassword("Password123!", "hash"))
            .Returns(true);

        var validator = new LoginCommandValidator();
        var handler = new LoginCommandHandler(
            _dbContextMock.Object,
            _tokenGeneratorMock.Object,
            _passwordHashMock.Object,
            validator);

        var command = new LoginCommand("user@shopsphere.local", "Password123!");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("Auth.UserInactive");
    }

    #endregion

    #region Refresh Token Tests

    [Fact]
    public async Task RefreshToken_WithValidToken_ShouldRotateTokenAndReturnNewTokenResponse()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "user@shopsphere.local", "hash", "Token User");
        _users.Add(user);

        var rawToken = "raw-refresh-token-123";
        var tokenHash = TokenSecurityHelper.HashRefreshToken(rawToken);
        var existingToken = RefreshToken.Create(Guid.NewGuid(), user.Id, tokenHash, DateTime.UtcNow.AddDays(7));
        _refreshTokens.Add(existingToken);

        _tokenGeneratorMock.Setup(x => x.GenerateToken(
                It.Is<User>(u => u.Id == user.Id),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<IReadOnlyCollection<string>>()))
            .Returns("new-access-token-456");

        var validator = new RefreshTokenCommandValidator();
        var handler = new RefreshTokenCommandHandler(
            _dbContextMock.Object,
            _tokenGeneratorMock.Object,
            validator);

        var command = new RefreshTokenCommand(rawToken);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("new-access-token-456");
        result.Value.RefreshToken.Should().NotBe(rawToken);

        existingToken.IsRevoked.Should().BeTrue();
        existingToken.ReplacedByTokenId.Should().NotBeNull();

        _refreshTokens.Should().HaveCount(2);
        var newToken = _refreshTokens.First(rt => rt.Id == existingToken.ReplacedByTokenId!.Value);
        newToken.UserId.Should().Be(user.Id);
        newToken.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshToken_WithRevokedToken_ShouldTriggerFamilyInvalidationAndReturnUnauthorized()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "user@shopsphere.local", "hash", "Compromised User");
        _users.Add(user);

        var rawCompromisedToken = "compromised-token";
        var compromisedHash = TokenSecurityHelper.HashRefreshToken(rawCompromisedToken);
        var revokedToken = RefreshToken.Create(Guid.NewGuid(), user.Id, compromisedHash, DateTime.UtcNow.AddDays(5));
        revokedToken.Revoke(DateTime.UtcNow.AddMinutes(-10), Guid.NewGuid());
        _refreshTokens.Add(revokedToken);

        // Active replacement token that should now be revoked due to family invalidation
        var activeSiblingToken = RefreshToken.Create(Guid.NewGuid(), user.Id, "active_hash", DateTime.UtcNow.AddDays(7));
        _refreshTokens.Add(activeSiblingToken);

        var validator = new RefreshTokenCommandValidator();
        var handler = new RefreshTokenCommandHandler(
            _dbContextMock.Object,
            _tokenGeneratorMock.Object,
            validator);

        var command = new RefreshTokenCommand(rawCompromisedToken);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("Auth.TokenRevoked");

        activeSiblingToken.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshToken_WithExpiredToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "user@shopsphere.local", "hash", "Expired Token User");
        _users.Add(user);

        var rawToken = "expired-token";
        var tokenHash = TokenSecurityHelper.HashRefreshToken(rawToken);
        // Created with future date, then expires
        var expiredToken = RefreshToken.Create(Guid.NewGuid(), user.Id, tokenHash, DateTime.UtcNow.AddMinutes(1));
        typeof(RefreshToken).GetProperty(nameof(RefreshToken.ExpiresAtUtc))!
            .SetValue(expiredToken, DateTime.UtcNow.AddHours(-1));
        _refreshTokens.Add(expiredToken);

        var validator = new RefreshTokenCommandValidator();
        var handler = new RefreshTokenCommandHandler(
            _dbContextMock.Object,
            _tokenGeneratorMock.Object,
            validator);

        var command = new RefreshTokenCommand(rawToken);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("Auth.TokenExpired");
    }

    [Fact]
    public async Task RefreshToken_WithUnknownToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var validator = new RefreshTokenCommandValidator();
        var handler = new RefreshTokenCommandHandler(
            _dbContextMock.Object,
            _tokenGeneratorMock.Object,
            validator);

        var command = new RefreshTokenCommand("non-existent-token");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("Auth.InvalidToken");
    }

    #endregion

    #region Revoke Token Tests

    [Fact]
    public async Task RevokeToken_SingleToken_ShouldRevokeMatchingToken()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var rawToken = "token-to-revoke";
        var tokenHash = TokenSecurityHelper.HashRefreshToken(rawToken);
        var token = RefreshToken.Create(Guid.NewGuid(), userId, tokenHash, DateTime.UtcNow.AddDays(1));
        _refreshTokens.Add(token);

        var handler = new RevokeTokenCommandHandler(_dbContextMock.Object);
        var command = new RevokeTokenCommand(userId, RefreshToken: rawToken, RevokeAll: false);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        token.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeToken_RevokeAll_ShouldRevokeAllUserTokens()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token1 = RefreshToken.Create(Guid.NewGuid(), userId, "hash1", DateTime.UtcNow.AddDays(1));
        var token2 = RefreshToken.Create(Guid.NewGuid(), userId, "hash2", DateTime.UtcNow.AddDays(2));
        var otherUserToken = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "hash3", DateTime.UtcNow.AddDays(2));

        _refreshTokens.AddRange(new[] { token1, token2, otherUserToken });

        var handler = new RevokeTokenCommandHandler(_dbContextMock.Object);
        var command = new RevokeTokenCommand(userId, RevokeAll: true);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        token1.IsRevoked.Should().BeTrue();
        token2.IsRevoked.Should().BeTrue();
        otherUserToken.IsRevoked.Should().BeFalse();
    }

    #endregion

    #region GetCurrentUser Tests

    [Fact]
    public async Task GetCurrentUser_WhenUserExists_ShouldReturnUserProfileDto()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "me@shopsphere.local", "hash", "Myself");
        var role = Role.Create(Guid.NewGuid(), "Admin", "Admin Role");
        var perm = Permission.Create(Guid.NewGuid(), "users:read", "Read users");
        role.AddPermission(perm.Id);
        user.AssignRole(role.Id);

        _users.Add(user);
        _roles.Add(role);
        _permissions.Add(perm);
        _userRoles.Add(new UserRole(user.Id, role.Id));
        _rolePermissions.Add(new RolePermission(role.Id, perm.Id));

        var handler = new GetCurrentUserQueryHandler(_dbContextMock.Object);
        var query = new GetCurrentUserQuery(user.Id);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
        result.Value.Email.Should().Be("me@shopsphere.local");
        result.Value.Roles.Should().Contain("Admin");
        result.Value.Permissions.Should().Contain("users:read");
    }

    [Fact]
    public async Task GetCurrentUser_WhenUserNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var handler = new GetCurrentUserQueryHandler(_dbContextMock.Object);
        var query = new GetCurrentUserQuery(Guid.NewGuid());

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("User.NotFound");
    }

    #endregion

    #region User Management Tests

    [Fact]
    public async Task AssignRole_WhenUserAndRoleExist_ShouldAssignRoleSuccessfully()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "user@shopsphere.local", "hash", "Role Assignee");
        var role = Role.Create(Guid.NewGuid(), "StoreManager", "Store Manager Role");
        _users.Add(user);
        _roles.Add(role);

        var handler = new AssignRoleCommandHandler(_dbContextMock.Object);
        var command = new AssignRoleCommand(user.Id, role.Id);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.UserRoles.Should().ContainSingle(ur => ur.RoleId == role.Id);
    }

    [Fact]
    public async Task AssignRole_WhenUserNotFound_ShouldReturnNotFound()
    {
        // Arrange
        var role = Role.Create(Guid.NewGuid(), "Admin", "Admin Role");
        _roles.Add(role);

        var handler = new AssignRoleCommandHandler(_dbContextMock.Object);
        var command = new AssignRoleCommand(Guid.NewGuid(), role.Id);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("User.NotFound");
    }

    [Fact]
    public async Task RemoveRole_WhenUserHasRole_ShouldRemoveRoleSuccessfully()
    {
        // Arrange
        var user = User.Create(Guid.NewGuid(), "user@shopsphere.local", "hash", "Role Holder");
        var roleId = Guid.NewGuid();
        user.AssignRole(roleId);
        _users.Add(user);

        var handler = new RemoveRoleCommandHandler(_dbContextMock.Object);
        var command = new RemoveRoleCommand(user.Id, roleId);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.UserRoles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUsers_ShouldReturnPaginatedUsers()
    {
        // Arrange
        for (int i = 1; i <= 5; i++)
        {
            _users.Add(User.Create(Guid.NewGuid(), $"user{i}@shopsphere.local", "hash", $"User {i}"));
        }

        var handler = new GetUsersQueryHandler(_dbContextMock.Object);
        var query = new GetUsersQuery(Page: 1, PageSize: 3);

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(3);
        result.Value.TotalCount.Should().Be(5);
        result.Value.Page.Should().Be(1);
        result.Value.PageSize.Should().Be(3);
    }

    [Fact]
    public async Task GetRoles_ShouldReturnRolesWithPermissions()
    {
        // Arrange
        var role = Role.Create(Guid.NewGuid(), "Admin", "Admin Role");
        var perm = Permission.Create(Guid.NewGuid(), "inventory:adjust", "Adjust inventory");
        role.AddPermission(perm.Id);

        _roles.Add(role);
        _permissions.Add(perm);
        _rolePermissions.Add(new RolePermission(role.Id, perm.Id));

        var handler = new GetRolesQueryHandler(_dbContextMock.Object);
        var query = new GetRolesQuery();

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(r => r.Name == "Admin");
        result.Value.First(r => r.Name == "Admin").Permissions.Should().Contain("inventory:adjust");
    }

    #endregion
}

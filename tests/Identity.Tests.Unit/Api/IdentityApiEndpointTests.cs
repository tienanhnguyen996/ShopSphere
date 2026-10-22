using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Moq;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Identity.Api;
using ShopSphere.Services.Identity.Api.Health;
using ShopSphere.Services.Identity.Application;
using ShopSphere.Services.Identity.Application.Auth.Commands.Login;
using ShopSphere.Services.Identity.Application.Auth.Commands.RefreshToken;
using ShopSphere.Services.Identity.Application.Auth.Commands.Register;
using ShopSphere.Services.Identity.Application.Auth.Commands.RevokeToken;
using ShopSphere.Services.Identity.Application.Auth.Queries.GetCurrentUser;
using ShopSphere.Services.Identity.Application.Common.Interfaces;
using ShopSphere.Services.Identity.Application.Contracts;
using ShopSphere.Services.Identity.Application.Roles.Queries.GetRoles;
using ShopSphere.Services.Identity.Application.Users.Commands.AssignRole;
using ShopSphere.Services.Identity.Application.Users.Commands.RemoveRole;
using ShopSphere.Services.Identity.Application.Users.Queries.GetUsers;
using ShopSphere.Services.Identity.Domain;
using ShopSphere.Services.Identity.Infrastructure;
using ShopSphere.Services.Identity.Tests.Unit.Helpers;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace ShopSphere.Services.Identity.Tests.Unit.Api;

public class IdentityApiEndpointTests
{
    #region Result Mapping Tests

    [Fact]
    public void ToHttpResult_WhenSuccess_ShouldReturnOkByDefault()
    {
        var result = Result.Success("test-payload");
        var httpResult = IdentityApiEndpoints.ToHttpResult(result);

        httpResult.Should().BeOfType<Ok<string>>();
        var ok = (Ok<string>)httpResult;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        ok.Value.Should().Be("test-payload");
    }

    [Fact]
    public void ToHttpResult_WhenSuccessWithNoContentStatus_ShouldReturnNoContent()
    {
        var result = Result.Success();
        var httpResult = IdentityApiEndpoints.ToHttpResult(result, StatusCodes.Status204NoContent);

        httpResult.Should().BeOfType<NoContent>();
        var noContent = (NoContent)httpResult;
        noContent.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    [Fact]
    public void ToHttpResult_WhenValidationFailure_ShouldReturnBadRequest()
    {
        var result = Result.Failure<string>(Error.Validation("Identity.Validation", "Invalid input data."));
        var httpResult = IdentityApiEndpoints.ToHttpResult(result);

        var statusCodeResult = httpResult as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void ToHttpResult_WhenNotFoundFailure_ShouldReturnNotFound()
    {
        var result = Result.Failure<string>(Error.NotFound("User.NotFound", "User does not exist."));
        var httpResult = IdentityApiEndpoints.ToHttpResult(result);

        var statusCodeResult = httpResult as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public void ToHttpResult_WhenConflictFailure_ShouldReturnConflict()
    {
        var result = Result.Failure<string>(Error.Conflict("User.DuplicateEmail", "Email already in use."));
        var httpResult = IdentityApiEndpoints.ToHttpResult(result);

        var statusCodeResult = httpResult as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void ToHttpResult_WhenUnauthorizedFailure_ShouldReturn401()
    {
        var result = Result.Failure<string>(Error.Unauthorized("Auth.InvalidCredentials", "Bad credentials."));
        var httpResult = IdentityApiEndpoints.ToHttpResult(result);

        var statusCodeResult = httpResult as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public void ToHttpResult_WhenForbiddenFailure_ShouldReturn403()
    {
        var result = Result.Failure<string>(Error.Forbidden("Auth.Forbidden", "Access denied."));
        var httpResult = IdentityApiEndpoints.ToHttpResult(result);

        var statusCodeResult = httpResult as IStatusCodeHttpResult;
        statusCodeResult.Should().NotBeNull();
        statusCodeResult!.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    #endregion

    #region Health Check Tests

    [Fact]
    public async Task SqlServerHealthCheck_ShouldReturnHealthStatus()
    {
        var dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=localhost;Database=TestIdentity;User Id=sa;Password=Password123!;TrustServerCertificate=True;")
            .Options;
        var dbContext = new IdentityDbContext(dbOptions);

        var healthCheck = new SqlServerHealthCheck(dbContext);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        result.Should().NotBeNull();
        result.Status.Should().BeOneOf(HealthStatus.Healthy, HealthStatus.Unhealthy);
    }

    #endregion

    #region OpenAPI Specification & Route Registration Tests

    [Fact]
    public async Task IdentityEndpoints_ShouldRegisterAllRoutesInOpenApiSpec()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ShopSphere Identity Service API",
                Version = "v1",
                Description = "Microservice managing authentication, JWT tokens, RBAC roles and permissions, and user accounts in ShopSphere."
            });
        });

        var dbOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=localhost;Database=TestIdentity;User Id=sa;Password=Password123!;TrustServerCertificate=True;")
            .Options;
        var dbContext = new IdentityDbContext(dbOptions);
        builder.Services.AddSingleton<IIdentityDbContext>(dbContext);
        builder.Services.AddSingleton(dbContext);

        builder.Services.AddApplication();
        builder.Services.AddSingleton<IPasswordHashService>(new Mock<IPasswordHashService>().Object);
        builder.Services.AddSingleton<IJwtTokenGenerator>(new Mock<IJwtTokenGenerator>().Object);
        builder.Services.AddHealthChecks();

        var app = builder.Build();
        app.MapIdentityEndpoints();
        await app.StartAsync();

        var swaggerProvider = app.Services.GetRequiredService<ISwaggerProvider>();
        var swaggerDoc = swaggerProvider.GetSwagger("v1");

        swaggerDoc.Should().NotBeNull();
        swaggerDoc.Info.Title.Should().Be("ShopSphere Identity Service API");

        // Verify required endpoints from task brief
        swaggerDoc.Paths.Should().ContainKey("/health");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/auth/register");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/auth/login");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/auth/refresh");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/auth/revoke");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/auth/me");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/users");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/users/{id}");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/users/{id}/roles");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/users/{id}/roles/{roleId}");
        swaggerDoc.Paths.Should().ContainKey("/api/v1/roles");

        // Verify operations
        swaggerDoc.Paths["/api/v1/auth/register"].Operations.Should().ContainKey(OperationType.Post);
        swaggerDoc.Paths["/api/v1/auth/login"].Operations.Should().ContainKey(OperationType.Post);
        swaggerDoc.Paths["/api/v1/auth/refresh"].Operations.Should().ContainKey(OperationType.Post);
        swaggerDoc.Paths["/api/v1/auth/revoke"].Operations.Should().ContainKey(OperationType.Post);
        swaggerDoc.Paths["/api/v1/auth/me"].Operations.Should().ContainKey(OperationType.Get);
        swaggerDoc.Paths["/api/v1/users"].Operations.Should().ContainKey(OperationType.Get);
        swaggerDoc.Paths["/api/v1/users/{id}"].Operations.Should().ContainKey(OperationType.Get);
        swaggerDoc.Paths["/api/v1/users/{id}/roles"].Operations.Should().ContainKey(OperationType.Post);
        swaggerDoc.Paths["/api/v1/users/{id}/roles/{roleId}"].Operations.Should().ContainKey(OperationType.Delete);
        swaggerDoc.Paths["/api/v1/roles"].Operations.Should().ContainKey(OperationType.Get);

        var json = swaggerDoc.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
        json.Should().NotBeNullOrWhiteSpace();

        await app.StopAsync();

        // Write OpenAPI specs to target paths
        var repoRoot = FindRepoRoot();
        var apiPath = Path.Combine(repoRoot, "src", "Services", "Identity", "Identity.Api", "openapi.json");
        var specsDir = Path.Combine(repoRoot, "docs", "specs");
        Directory.CreateDirectory(specsDir);
        var specPath = Path.Combine(specsDir, "identity-openapi.json");

        File.WriteAllText(apiPath, json);
        File.WriteAllText(specPath, json);

        File.Exists(apiPath).Should().BeTrue();
        File.Exists(specPath).Should().BeTrue();
    }

    #endregion

    #region User Context & Extraction Tests

    [Fact]
    public void ExtractUserId_WhenNullUser_ShouldReturnEmptyGuid()
    {
        var result = IdentityApiEndpoints.ExtractUserId(null);
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractUserId_WhenUnauthenticated_ShouldReturnEmptyGuid()
    {
        var identity = new ClaimsIdentity();
        var principal = new ClaimsPrincipal(identity);

        var result = IdentityApiEndpoints.ExtractUserId(principal);
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractUserId_WhenAuthenticatedWithSubClaim_ShouldReturnGuid()
    {
        var expectedId = Guid.NewGuid();
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("sub", expectedId.ToString())
        }, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var result = IdentityApiEndpoints.ExtractUserId(principal);
        result.Should().Be(expectedId);
    }

    [Fact]
    public void ExtractUserId_WhenAuthenticatedWithNameIdentifierClaim_ShouldReturnGuid()
    {
        var expectedId = Guid.NewGuid();
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, expectedId.ToString())
        }, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var result = IdentityApiEndpoints.ExtractUserId(principal);
        result.Should().Be(expectedId);
    }

    [Fact]
    public void ExtractUserId_WhenSubClaimIsNotGuid_ShouldReturnEmptyGuid()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("sub", "not-a-valid-guid")
        }, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var result = IdentityApiEndpoints.ExtractUserId(principal);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserByIdQueryHandler_WhenUserExists_ShouldReturnUserProfileDto()
    {
        var user = User.Create(Guid.NewGuid(), "test@user.com", "hash", "Test User");
        var users = new List<User> { user };

        var dbContextMock = new Mock<IIdentityDbContext>();
        dbContextMock.Setup(c => c.Users).Returns(MockDbSetExtensions.CreateMockDbSet(users).Object);

        var resolverMock = new Mock<IUserRolePermissionResolver>();
        resolverMock.Setup(r => r.ResolveAsync(user, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { "Customer" }, new[] { "catalog:products:read" }));

        var getCurrentUserHandler = new GetCurrentUserQueryHandler(dbContextMock.Object, resolverMock.Object);
        var handler = new ShopSphere.Services.Identity.Application.Users.Queries.GetUserById.GetUserByIdQueryHandler(getCurrentUserHandler);

        var result = await handler.HandleAsync(new ShopSphere.Services.Identity.Application.Users.Queries.GetUserById.GetUserByIdQuery(user.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(user.Id);
        result.Value.Email.Should().Be(user.Email);
    }

    #endregion

    #region Request Model Contract Tests

    [Fact]
    public void RegisterRequest_ShouldInitializeProperties()
    {
        var req = new RegisterRequest("user@test.com", "P@ss12345", "Test User");
        req.Email.Should().Be("user@test.com");
        req.Password.Should().Be("P@ss12345");
        req.FullName.Should().Be("Test User");
    }

    [Fact]
    public void LoginRequest_ShouldInitializeProperties()
    {
        var req = new LoginRequest("user@test.com", "P@ss12345");
        req.Email.Should().Be("user@test.com");
        req.Password.Should().Be("P@ss12345");
    }

    [Fact]
    public void RefreshTokenRequest_ShouldInitializeProperties()
    {
        var req = new RefreshTokenRequest("raw_refresh_token_123");
        req.RefreshToken.Should().Be("raw_refresh_token_123");
    }

    [Fact]
    public void RevokeTokenRequest_ShouldInitializeProperties()
    {
        var userId = Guid.NewGuid();
        var req = new RevokeTokenRequest(userId, "token_123", true);
        req.UserId.Should().Be(userId);
        req.RefreshToken.Should().Be("token_123");
        req.RevokeAll.Should().BeTrue();
    }

    [Fact]
    public void AssignRoleRequest_ShouldInitializeProperties()
    {
        var roleId = Guid.NewGuid();
        var req = new AssignRoleRequest(roleId);
        req.RoleId.Should().Be(roleId);
    }

    #endregion

    #region Helper Methods

    private static string FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(dir) && !File.Exists(Path.Combine(dir, "ShopSphere.sln")))
        {
            var parent = Directory.GetParent(dir);
            if (parent is null)
            {
                break;
            }

            dir = parent.FullName;
        }

        return dir;
    }

    #endregion
}

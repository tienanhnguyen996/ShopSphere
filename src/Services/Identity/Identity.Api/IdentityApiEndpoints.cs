using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Identity.Application.Auth.Commands.Login;
using ShopSphere.Services.Identity.Application.Auth.Commands.RefreshToken;
using ShopSphere.Services.Identity.Application.Auth.Commands.Register;
using ShopSphere.Services.Identity.Application.Auth.Commands.RevokeToken;
using ShopSphere.Services.Identity.Application.Auth.Queries.GetCurrentUser;
using ShopSphere.Services.Identity.Application.Common.Models;
using ShopSphere.Services.Identity.Application.Contracts;
using ShopSphere.Services.Identity.Application.Roles.Queries.GetRoles;
using ShopSphere.Services.Identity.Application.Users.Commands.AssignRole;
using ShopSphere.Services.Identity.Application.Users.Commands.RemoveRole;
using ShopSphere.Services.Identity.Application.Users.Queries.GetUserById;
using ShopSphere.Services.Identity.Application.Users.Queries.GetUsers;

namespace ShopSphere.Services.Identity.Api;

public record RegisterRequest(string Email, string Password, string FullName);
public record LoginRequest(string Email, string Password);
public record RefreshTokenRequest(string RefreshToken);
public record RevokeTokenRequest(Guid? UserId = null, string? RefreshToken = null, bool? RevokeAll = null);
public record AssignRoleRequest(Guid RoleId);

public static class IdentityApiEndpoints
{
    public const string UserIdHeader = "X-User-Id";

    public static WebApplication MapIdentityEndpoints(this WebApplication app)
    {
        // GET /health
        app.MapGet("/health", async (
            HealthCheckService healthCheckService,
            CancellationToken ct) =>
        {
            var report = await healthCheckService.CheckHealthAsync(ct);
            var response = new
            {
                status = report.Status.ToString(),
                totalDuration = report.TotalDuration.ToString(),
                entries = report.Entries.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new
                    {
                        status = kvp.Value.Status.ToString(),
                        description = kvp.Value.Description,
                        duration = kvp.Value.Duration.ToString(),
                        error = kvp.Value.Exception?.Message
                    })
            };

            var statusCode = report.Status != HealthStatus.Unhealthy
                ? StatusCodes.Status200OK
                : StatusCodes.Status503ServiceUnavailable;

            return Results.Json(response, statusCode: statusCode);
        })
        .WithName("HealthCheck")
        .WithTags("Health")
        .WithSummary("Returns health status of Identity Service dependencies (SQL Server)")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status503ServiceUnavailable)
        .WithOpenApi();

        // Auth endpoints
        var authGroup = app.MapGroup("/api/v1/auth")
            .WithTags("Auth");

        // POST /api/v1/auth/register
        authGroup.MapPost("/register", async (
            [FromBody] RegisterRequest request,
            RegisterCommandHandler handler,
            CancellationToken ct) =>
        {
            var command = new RegisterCommand(request.Email, request.Password, request.FullName);
            var result = await handler.HandleAsync(command, ct);
            return ToHttpResult(result, StatusCodes.Status200OK);
        })
        .WithName("Register")
        .WithSummary("Register a new user account and obtain access/refresh tokens")
        .Produces<TokenResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict)
        .WithOpenApi();

        // POST /api/v1/auth/login
        authGroup.MapPost("/login", async (
            [FromBody] LoginRequest request,
            LoginCommandHandler handler,
            CancellationToken ct) =>
        {
            var command = new LoginCommand(request.Email, request.Password);
            var result = await handler.HandleAsync(command, ct);
            return ToHttpResult(result, StatusCodes.Status200OK);
        })
        .WithName("Login")
        .WithSummary("Authenticate with user credentials and obtain access/refresh tokens")
        .Produces<TokenResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .WithOpenApi();

        // POST /api/v1/auth/refresh
        authGroup.MapPost("/refresh", async (
            [FromBody] RefreshTokenRequest request,
            RefreshTokenCommandHandler handler,
            CancellationToken ct) =>
        {
            var command = new RefreshTokenCommand(request.RefreshToken);
            var result = await handler.HandleAsync(command, ct);
            return ToHttpResult(result, StatusCodes.Status200OK);
        })
        .WithName("RefreshToken")
        .WithSummary("Rotate refresh token and obtain a fresh access token")
        .Produces<TokenResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .WithOpenApi();

        // POST /api/v1/auth/revoke
        authGroup.MapPost("/revoke", async (
            [FromBody] RevokeTokenRequest? request,
            HttpContext context,
            RevokeTokenCommandHandler handler,
            CancellationToken ct) =>
        {
            var userId = ExtractUserId(context.User);
            if (userId == Guid.Empty && context.Request.Headers.TryGetValue(UserIdHeader, out var headerVal) && Guid.TryParse(headerVal, out var parsedHeaderId))
            {
                userId = parsedHeaderId;
            }
            if (userId == Guid.Empty && request?.UserId is not null && request.UserId.Value != Guid.Empty)
            {
                userId = request.UserId.Value;
            }

            if (userId == Guid.Empty)
            {
                return Results.Json(new { error = "UserId is required to revoke tokens.", code = "Auth.UserIdRequired" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var command = new RevokeTokenCommand(userId, request?.RefreshToken, request?.RevokeAll ?? false);
            var result = await handler.HandleAsync(command, ct);
            return ToHttpResult(result, StatusCodes.Status204NoContent);
        })
        .WithName("RevokeToken")
        .WithSummary("Revoke a refresh token or all active tokens for a user")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .WithOpenApi();

        // GET /api/v1/auth/me
        authGroup.MapGet("/me", async (
            HttpContext context,
            GetCurrentUserQueryHandler handler,
            CancellationToken ct) =>
        {
            var userId = ExtractUserId(context.User);
            if (userId == Guid.Empty && context.Request.Headers.TryGetValue(UserIdHeader, out var headerVal) && Guid.TryParse(headerVal, out var parsedHeaderId))
            {
                userId = parsedHeaderId;
            }

            if (userId == Guid.Empty)
            {
                return Results.Json(new { error = "Unauthorized access.", code = "Auth.Unauthorized" }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var query = new GetCurrentUserQuery(userId);
            var result = await handler.HandleAsync(query, ct);
            return ToHttpResult(result, StatusCodes.Status200OK);
        })
        .WithName("GetCurrentUser")
        .WithSummary("Retrieve user profile, roles, and permissions for the currently authenticated user")
        .Produces<UserProfileDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        // Users endpoints
        var usersGroup = app.MapGroup("/api/v1/users")
            .WithTags("Users");

        // GET /api/v1/users
        usersGroup.MapGet("/", async (
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            GetUsersQueryHandler handler,
            CancellationToken ct) =>
        {
            var query = new GetUsersQuery(page ?? 1, pageSize ?? 20, search);
            var result = await handler.HandleAsync(query, ct);
            return ToHttpResult(result, StatusCodes.Status200OK);
        })
        .WithName("GetUsers")
        .WithSummary("Retrieve a paginated list of users with role and permission summaries")
        .Produces<PagedResult<UserProfileDto>>(StatusCodes.Status200OK)
        .WithOpenApi();

        // GET /api/v1/users/{id:guid}
        usersGroup.MapGet("/{id:guid}", async (
            Guid id,
            GetUserByIdQueryHandler handler,
            CancellationToken ct) =>
        {
            var query = new GetUserByIdQuery(id);
            var result = await handler.HandleAsync(query, ct);
            return ToHttpResult(result, StatusCodes.Status200OK);
        })
        .WithName("GetUserById")
        .WithSummary("Retrieve user details and assigned roles/permissions by user ID")
        .Produces<UserProfileDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        // POST /api/v1/users/{id:guid}/roles
        usersGroup.MapPost("/{id:guid}/roles", async (
            Guid id,
            [FromBody] AssignRoleRequest request,
            AssignRoleCommandHandler handler,
            CancellationToken ct) =>
        {
            var command = new AssignRoleCommand(id, request.RoleId);
            var result = await handler.HandleAsync(command, ct);
            return ToHttpResult(result, StatusCodes.Status204NoContent);
        })
        .WithName("AssignRole")
        .WithSummary("Assign a role to a user")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        // DELETE /api/v1/users/{id:guid}/roles/{roleId:guid}
        usersGroup.MapDelete("/{id:guid}/roles/{roleId:guid}", async (
            Guid id,
            Guid roleId,
            RemoveRoleCommandHandler handler,
            CancellationToken ct) =>
        {
            var command = new RemoveRoleCommand(id, roleId);
            var result = await handler.HandleAsync(command, ct);
            return ToHttpResult(result, StatusCodes.Status204NoContent);
        })
        .WithName("RemoveRole")
        .WithSummary("Remove a role assignment from a user")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .WithOpenApi();

        // Roles endpoints
        var rolesGroup = app.MapGroup("/api/v1/roles")
            .WithTags("Roles");

        // GET /api/v1/roles
        rolesGroup.MapGet("/", async (
            GetRolesQueryHandler handler,
            CancellationToken ct) =>
        {
            var query = new GetRolesQuery();
            var result = await handler.HandleAsync(query, ct);
            return ToHttpResult(result, StatusCodes.Status200OK);
        })
        .WithName("GetRoles")
        .WithSummary("Retrieve all roles with their assigned permissions")
        .Produces<IReadOnlyList<RoleDto>>(StatusCodes.Status200OK)
        .WithOpenApi();

        return app;
    }

    public static Guid ExtractUserId(ClaimsPrincipal? user)
    {
        if (user is null || user.Identity?.IsAuthenticated != true)
        {
            return Guid.Empty;
        }

        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    public static IResult ToHttpResult<T>(Result<T> result, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return successStatusCode switch
            {
                StatusCodes.Status204NoContent => Results.NoContent(),
                StatusCodes.Status201Created => Results.Created(string.Empty, result.Value),
                _ => Results.Ok(result.Value)
            };
        }

        return MapErrorToHttpResult(result.Error);
    }

    public static IResult ToHttpResult(Result result, int successStatusCode = StatusCodes.Status200OK)
    {
        if (result.IsSuccess)
        {
            return successStatusCode switch
            {
                StatusCodes.Status204NoContent => Results.NoContent(),
                _ => Results.Ok()
            };
        }

        return MapErrorToHttpResult(result.Error);
    }

    private static IResult MapErrorToHttpResult(Error error)
    {
        return error.Type switch
        {
            ErrorType.NotFound => Results.NotFound(new { error = error.Description, code = error.Code }),
            ErrorType.Validation => Results.BadRequest(new { error = error.Description, code = error.Code }),
            ErrorType.Conflict => Results.Conflict(new { error = error.Description, code = error.Code }),
            ErrorType.Unauthorized => Results.Json(new { error = error.Description, code = error.Code }, statusCode: StatusCodes.Status401Unauthorized),
            ErrorType.Forbidden => Results.Json(new { error = error.Description, code = error.Code }, statusCode: StatusCodes.Status403Forbidden),
            _ => Results.Problem(detail: error.Description, statusCode: StatusCodes.Status500InternalServerError, title: error.Code)
        };
    }
}

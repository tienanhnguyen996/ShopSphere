using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Identity.Application.Common.Interfaces;
using ShopSphere.Services.Identity.Application.Contracts;
using ShopSphere.Services.Identity.Domain;

namespace ShopSphere.Services.Identity.Application.Auth.Queries.GetCurrentUser;

public class GetCurrentUserQueryHandler
{
    private readonly IIdentityDbContext _dbContext;

    public GetCurrentUserQueryHandler(IIdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<UserProfileDto>> HandleAsync(GetCurrentUserQuery query, CancellationToken ct = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == query.UserId, ct);

        if (user is null)
        {
            return Result.Failure<UserProfileDto>(Error.NotFound(
                "User.NotFound",
                $"User '{query.UserId}' was not found."));
        }

        var (roles, permissions) = await ResolveRolesAndPermissionsAsync(user, ct);

        return Result.Success(new UserProfileDto(
            user.Id,
            user.Email,
            user.FullName,
            user.IsActive,
            roles,
            permissions,
            user.CreatedAtUtc));
    }

    private async Task<(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)> ResolveRolesAndPermissionsAsync(User user, CancellationToken ct)
    {
        var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();
        if (roleIds.Count == 0)
        {
            roleIds = await _dbContext.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .Select(ur => ur.RoleId)
                .ToListAsync(ct);
        }

        if (roleIds.Count == 0)
        {
            return (Array.Empty<string>(), Array.Empty<string>());
        }

        var roles = await _dbContext.Roles
            .Where(r => roleIds.Contains(r.Id))
            .ToListAsync(ct);
        var roleNames = roles.Select(r => r.Name).Distinct().ToList();

        var permissionIds = await _dbContext.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.PermissionId)
            .Distinct()
            .ToListAsync(ct);

        var permissions = await _dbContext.Permissions
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => p.Code)
            .Distinct()
            .ToListAsync(ct);

        return (roleNames, permissions);
    }
}

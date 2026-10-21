using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Identity.Application.Common.Interfaces;
using ShopSphere.Services.Identity.Application.Common.Models;
using ShopSphere.Services.Identity.Application.Contracts;
using ShopSphere.Services.Identity.Domain;

namespace ShopSphere.Services.Identity.Application.Users.Queries.GetUsers;

public class GetUsersQueryHandler
{
    private readonly IIdentityDbContext _dbContext;

    public GetUsersQueryHandler(IIdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedResult<UserProfileDto>>> HandleAsync(GetUsersQuery query, CancellationToken ct = default)
    {
        var usersQuery = _dbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var search = query.SearchTerm.Trim().ToLowerInvariant();
            usersQuery = usersQuery.Where(u => u.Email.Contains(search) || u.FullName.ToLower().Contains(search));
        }

        var totalCount = await usersQuery.CountAsync(ct);

        var users = await usersQuery
            .OrderBy(u => u.Email)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var dtos = new List<UserProfileDto>();
        foreach (var user in users)
        {
            var (roles, permissions) = await ResolveRolesAndPermissionsAsync(user, ct);
            dtos.Add(new UserProfileDto(
                user.Id,
                user.Email,
                user.FullName,
                user.IsActive,
                roles,
                permissions,
                user.CreatedAtUtc));
        }

        return Result.Success(new PagedResult<UserProfileDto>(dtos, totalCount, query.Page, query.PageSize));
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

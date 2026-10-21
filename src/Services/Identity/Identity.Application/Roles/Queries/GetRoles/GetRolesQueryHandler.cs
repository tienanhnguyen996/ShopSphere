using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Identity.Application.Common.Interfaces;
using ShopSphere.Services.Identity.Application.Contracts;

namespace ShopSphere.Services.Identity.Application.Roles.Queries.GetRoles;

public class GetRolesQueryHandler
{
    private readonly IIdentityDbContext _dbContext;

    public GetRolesQueryHandler(IIdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<IReadOnlyList<RoleDto>>> HandleAsync(GetRolesQuery query, CancellationToken ct = default)
    {
        var roles = await _dbContext.Roles
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        var allRolePermissions = await _dbContext.RolePermissions
            .ToListAsync(ct);

        var allPermissions = await _dbContext.Permissions
            .ToListAsync(ct);

        var permissionDict = allPermissions.ToDictionary(p => p.Id, p => p.Code);

        var dtos = new List<RoleDto>();
        foreach (var role in roles)
        {
            var permissionIds = role.RolePermissions.Select(rp => rp.PermissionId).ToList();
            if (permissionIds.Count == 0)
            {
                permissionIds = allRolePermissions
                    .Where(rp => rp.RoleId == role.Id)
                    .Select(rp => rp.PermissionId)
                    .ToList();
            }

            var permissionCodes = permissionIds
                .Where(id => permissionDict.ContainsKey(id))
                .Select(id => permissionDict[id])
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            dtos.Add(new RoleDto(
                role.Id,
                role.Name,
                role.Description,
                permissionCodes));
        }

        return Result.Success<IReadOnlyList<RoleDto>>(dtos);
    }
}

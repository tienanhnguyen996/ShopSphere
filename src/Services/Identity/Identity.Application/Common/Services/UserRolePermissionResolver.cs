using Microsoft.EntityFrameworkCore;
using ShopSphere.Services.Identity.Application.Common.Interfaces;
using ShopSphere.Services.Identity.Domain;

namespace ShopSphere.Services.Identity.Application.Common.Services;

public class UserRolePermissionResolver : IUserRolePermissionResolver
{
    private readonly IIdentityDbContext _dbContext;

    public UserRolePermissionResolver(IIdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)> ResolveAsync(
        User user,
        CancellationToken ct = default)
    {
        var batch = await ResolveBatchAsync(new[] { user }, ct);
        return batch.TryGetValue(user.Id, out var resolved)
            ? resolved
            : (Array.Empty<string>(), Array.Empty<string>());
    }

    public async Task<Dictionary<Guid, (IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)>> ResolveBatchAsync(
        IReadOnlyCollection<User> users,
        CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, (IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)>();
        if (users.Count == 0)
        {
            return result;
        }

        var userIds = users.Select(u => u.Id).ToList();

        var dbUserRoles = await _dbContext.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .ToListAsync(ct);

        var userRoleMap = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var user in users)
        {
            userRoleMap[user.Id] = new HashSet<Guid>(user.UserRoles.Select(ur => ur.RoleId));
        }

        foreach (var ur in dbUserRoles)
        {
            if (userRoleMap.TryGetValue(ur.UserId, out var set))
            {
                set.Add(ur.RoleId);
            }
        }

        var allRoleIds = userRoleMap.Values.SelectMany(x => x).Distinct().ToList();
        if (allRoleIds.Count == 0)
        {
            foreach (var user in users)
            {
                result[user.Id] = (Array.Empty<string>(), Array.Empty<string>());
            }
            return result;
        }

        var roles = await _dbContext.Roles
            .Where(r => allRoleIds.Contains(r.Id))
            .ToListAsync(ct);
        var roleDict = roles.ToDictionary(r => r.Id, r => r.Name);

        var rolePermissions = await _dbContext.RolePermissions
            .Where(rp => allRoleIds.Contains(rp.RoleId))
            .ToListAsync(ct);

        var rolePermissionMap = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var role in roles)
        {
            rolePermissionMap[role.Id] = new HashSet<Guid>(role.RolePermissions.Select(rp => rp.PermissionId));
        }

        foreach (var rp in rolePermissions)
        {
            if (!rolePermissionMap.TryGetValue(rp.RoleId, out var permSet))
            {
                permSet = new HashSet<Guid>();
                rolePermissionMap[rp.RoleId] = permSet;
            }
            permSet.Add(rp.PermissionId);
        }

        var allPermissionIds = rolePermissionMap.Values.SelectMany(x => x).Distinct().ToList();
        var permissions = await _dbContext.Permissions
            .Where(p => allPermissionIds.Contains(p.Id))
            .ToListAsync(ct);
        var permissionDict = permissions.ToDictionary(p => p.Id, p => p.Code);

        foreach (var user in users)
        {
            var roleIds = userRoleMap[user.Id];
            var roleNames = roleIds
                .Where(rid => roleDict.ContainsKey(rid))
                .Select(rid => roleDict[rid])
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            var permCodes = roleIds
                .Where(rid => rolePermissionMap.ContainsKey(rid))
                .SelectMany(rid => rolePermissionMap[rid])
                .Where(pid => permissionDict.ContainsKey(pid))
                .Select(pid => permissionDict[pid])
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            result[user.Id] = (roleNames, permCodes);
        }

        return result;
    }
}

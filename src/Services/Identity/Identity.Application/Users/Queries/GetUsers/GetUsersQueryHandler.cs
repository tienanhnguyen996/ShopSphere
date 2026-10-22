using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Identity.Application.Common.Interfaces;
using ShopSphere.Services.Identity.Application.Common.Models;
using ShopSphere.Services.Identity.Application.Contracts;

namespace ShopSphere.Services.Identity.Application.Users.Queries.GetUsers;

public class GetUsersQueryHandler
{
    private readonly IIdentityDbContext _dbContext;
    private readonly IUserRolePermissionResolver _rolePermissionResolver;

    public GetUsersQueryHandler(
        IIdentityDbContext dbContext,
        IUserRolePermissionResolver rolePermissionResolver)
    {
        _dbContext = dbContext;
        _rolePermissionResolver = rolePermissionResolver;
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

        var batchResolved = await _rolePermissionResolver.ResolveBatchAsync(users, ct);

        var dtos = new List<UserProfileDto>(users.Count);
        foreach (var user in users)
        {
            var (roles, permissions) = batchResolved.TryGetValue(user.Id, out var resolved)
                ? resolved
                : (Array.Empty<string>(), Array.Empty<string>());

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
}

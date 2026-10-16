using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Identity.Application.Common.Interfaces;
using ShopSphere.Services.Identity.Application.Contracts;

namespace ShopSphere.Services.Identity.Application.Auth.Queries.GetCurrentUser;

public class GetCurrentUserQueryHandler
{
    private readonly IIdentityDbContext _dbContext;
    private readonly IUserRolePermissionResolver _rolePermissionResolver;

    public GetCurrentUserQueryHandler(
        IIdentityDbContext dbContext,
        IUserRolePermissionResolver rolePermissionResolver)
    {
        _dbContext = dbContext;
        _rolePermissionResolver = rolePermissionResolver;
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

        var (roles, permissions) = await _rolePermissionResolver.ResolveAsync(user, ct);

        return Result.Success(new UserProfileDto(
            user.Id,
            user.Email,
            user.FullName,
            user.IsActive,
            roles,
            permissions,
            user.CreatedAtUtc));
    }
}

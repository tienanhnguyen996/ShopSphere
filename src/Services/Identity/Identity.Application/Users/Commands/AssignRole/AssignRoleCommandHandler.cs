using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Identity.Application.Common.Interfaces;
using ShopSphere.Services.Identity.Domain;

namespace ShopSphere.Services.Identity.Application.Users.Commands.AssignRole;

public class AssignRoleCommandHandler
{
    private readonly IIdentityDbContext _dbContext;

    public AssignRoleCommandHandler(IIdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> HandleAsync(AssignRoleCommand command, CancellationToken ct = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == command.UserId, ct);

        if (user is null)
        {
            return Result.Failure(Error.NotFound(
                "User.NotFound",
                $"User '{command.UserId}' was not found."));
        }

        var roleExists = await _dbContext.Roles
            .AnyAsync(r => r.Id == command.RoleId, ct);

        if (!roleExists)
        {
            return Result.Failure(Error.NotFound(
                "Role.NotFound",
                $"Role '{command.RoleId}' was not found."));
        }

        user.AssignRole(command.RoleId);

        var userRoleExists = await _dbContext.UserRoles
            .AnyAsync(ur => ur.UserId == command.UserId && ur.RoleId == command.RoleId, ct);

        if (!userRoleExists)
        {
            _dbContext.UserRoles.Add(new UserRole(command.UserId, command.RoleId));
        }

        await _dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}

using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Identity.Application.Common.Interfaces;

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
            .Include(u => u.UserRoles)
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

        await _dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}

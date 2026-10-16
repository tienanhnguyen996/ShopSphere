using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Identity.Application.Common.Interfaces;

namespace ShopSphere.Services.Identity.Application.Users.Commands.RemoveRole;

public class RemoveRoleCommandHandler
{
    private readonly IIdentityDbContext _dbContext;

    public RemoveRoleCommandHandler(IIdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> HandleAsync(RemoveRoleCommand command, CancellationToken ct = default)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == command.UserId, ct);

        if (user is null)
        {
            return Result.Failure(Error.NotFound(
                "User.NotFound",
                $"User '{command.UserId}' was not found."));
        }

        user.RemoveRole(command.RoleId);

        var userRole = await _dbContext.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == command.UserId && ur.RoleId == command.RoleId, ct);

        if (userRole is not null)
        {
            _dbContext.UserRoles.Remove(userRole);
        }

        await _dbContext.SaveChangesAsync(ct);

        return Result.Success();
    }
}

namespace ShopSphere.Services.Identity.Application.Users.Commands.AssignRole;

public sealed record AssignRoleCommand(
    Guid UserId,
    Guid RoleId);

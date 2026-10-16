namespace ShopSphere.Services.Identity.Application.Users.Commands.RemoveRole;

public sealed record RemoveRoleCommand(
    Guid UserId,
    Guid RoleId);

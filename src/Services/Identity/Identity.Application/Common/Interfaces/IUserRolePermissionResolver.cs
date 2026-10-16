using ShopSphere.Services.Identity.Domain;

namespace ShopSphere.Services.Identity.Application.Common.Interfaces;

public interface IUserRolePermissionResolver
{
    Task<(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)> ResolveAsync(
        User user,
        CancellationToken ct = default);

    Task<Dictionary<Guid, (IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)>> ResolveBatchAsync(
        IReadOnlyCollection<User> users,
        CancellationToken ct = default);
}

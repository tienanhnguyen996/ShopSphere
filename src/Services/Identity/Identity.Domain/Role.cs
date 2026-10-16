using ShopSphere.BuildingBlocks.Common.Domain;
using ShopSphere.Services.Identity.Domain.Exceptions;

namespace ShopSphere.Services.Identity.Domain;

public class Role : Entity<Guid>
{
    private readonly List<RolePermission> _rolePermissions = new();

    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions.AsReadOnly();

    // EF Core parameterless constructor
    private Role() : base() { }

    public Role(Guid id, string name, string description) : base()
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("Role identifier cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new IdentityDomainException("Role name cannot be empty.");
        }

        Id = id;
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
    }

    public static Role Create(Guid id, string name, string description)
    {
        return new Role(id, name, description);
    }

    public void AddPermission(Guid permissionId)
    {
        if (permissionId == Guid.Empty)
        {
            throw new IdentityDomainException("Permission identifier cannot be empty.");
        }

        if (_rolePermissions.Any(rp => rp.PermissionId == permissionId))
        {
            return;
        }

        _rolePermissions.Add(new RolePermission(Id, permissionId));
    }

    public void RemovePermission(Guid permissionId)
    {
        if (permissionId == Guid.Empty)
        {
            throw new IdentityDomainException("Permission identifier cannot be empty.");
        }

        var existing = _rolePermissions.FirstOrDefault(rp => rp.PermissionId == permissionId);
        if (existing is not null)
        {
            _rolePermissions.Remove(existing);
        }
    }
}

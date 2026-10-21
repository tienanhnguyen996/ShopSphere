using ShopSphere.Services.Identity.Domain.Exceptions;

namespace ShopSphere.Services.Identity.Domain;

public class RolePermission
{
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }

    // Navigation properties for EF Core
    public Role? Role { get; private set; }
    public Permission? Permission { get; private set; }

    // EF Core parameterless constructor
    private RolePermission() { }

    public RolePermission(Guid roleId, Guid permissionId)
    {
        if (roleId == Guid.Empty)
        {
            throw new IdentityDomainException("RoleId cannot be empty.");
        }

        if (permissionId == Guid.Empty)
        {
            throw new IdentityDomainException("PermissionId cannot be empty.");
        }

        RoleId = roleId;
        PermissionId = permissionId;
    }
}

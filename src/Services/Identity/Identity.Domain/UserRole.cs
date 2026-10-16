using ShopSphere.Services.Identity.Domain.Exceptions;

namespace ShopSphere.Services.Identity.Domain;

public class UserRole
{
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }

    // Navigation properties for EF Core
    public User? User { get; private set; }
    public Role? Role { get; private set; }

    // EF Core parameterless constructor
    private UserRole() { }

    public UserRole(Guid userId, Guid roleId)
    {
        if (userId == Guid.Empty)
        {
            throw new IdentityDomainException("UserId cannot be empty.");
        }

        if (roleId == Guid.Empty)
        {
            throw new IdentityDomainException("RoleId cannot be empty.");
        }

        UserId = userId;
        RoleId = roleId;
    }
}

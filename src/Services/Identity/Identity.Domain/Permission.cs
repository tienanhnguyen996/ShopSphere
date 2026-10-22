using ShopSphere.BuildingBlocks.Common.Domain;
using ShopSphere.Services.Identity.Domain.Exceptions;

namespace ShopSphere.Services.Identity.Domain;

public class Permission : Entity<Guid>
{
    public string Code { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    // EF Core parameterless constructor
    private Permission() : base() { }

    public Permission(Guid id, string code, string description) : base()
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("Permission identifier cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new IdentityDomainException("Permission code cannot be empty.");
        }

        Id = id;
        Code = code.Trim();
        Description = description?.Trim() ?? string.Empty;
    }

    public static Permission Create(Guid id, string code, string description)
    {
        return new Permission(id, code, description);
    }
}

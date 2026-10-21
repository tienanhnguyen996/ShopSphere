using ShopSphere.BuildingBlocks.Common.Domain;
using ShopSphere.Services.Identity.Domain.Exceptions;

namespace ShopSphere.Services.Identity.Domain;

public class User : AggregateRoot<Guid>, IAuditableEntity
{
    private readonly List<UserRole> _userRoles = new();

    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();

    // EF Core parameterless constructor
    private User() : base() { }

    public User(Guid id, string email, string passwordHash, string fullName) : base()
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("User identifier cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new IdentityDomainException("User email cannot be empty.");
        }

        var trimmedEmail = email.Trim();
        var atIndex = trimmedEmail.IndexOf('@');
        if (atIndex <= 0 || atIndex >= trimmedEmail.Length - 1 || trimmedEmail.IndexOf('@', atIndex + 1) != -1)
        {
            throw new IdentityDomainException("User email is invalid.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new IdentityDomainException("User password hash cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new IdentityDomainException("User full name cannot be empty.");
        }

        Id = id;
        Email = trimmedEmail.ToLowerInvariant();
        PasswordHash = passwordHash;
        FullName = fullName.Trim();
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static User Create(Guid id, string email, string passwordHash, string fullName)
    {
        return new User(id, email, passwordHash, fullName);
    }

    public void AssignRole(Guid roleId)
    {
        if (roleId == Guid.Empty)
        {
            throw new IdentityDomainException("RoleId cannot be empty.");
        }

        if (_userRoles.Any(ur => ur.RoleId == roleId))
        {
            return;
        }

        _userRoles.Add(new UserRole(Id, roleId));
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RemoveRole(Guid roleId)
    {
        if (roleId == Guid.Empty)
        {
            throw new IdentityDomainException("RoleId cannot be empty.");
        }

        var existing = _userRoles.FirstOrDefault(ur => ur.RoleId == roleId);
        if (existing is not null)
        {
            _userRoles.Remove(existing);
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdatePasswordHash(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            throw new IdentityDomainException("User password hash cannot be empty.");
        }

        PasswordHash = newPasswordHash;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateProfile(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new IdentityDomainException("User full name cannot be empty.");
        }

        FullName = fullName.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

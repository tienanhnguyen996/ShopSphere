using ShopSphere.BuildingBlocks.Common.Domain;
using ShopSphere.Services.Identity.Domain.Exceptions;

namespace ShopSphere.Services.Identity.Domain;

public class RefreshToken : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsActive => RevokedAtUtc == null && DateTime.UtcNow < ExpiresAtUtc;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsRevoked => RevokedAtUtc.HasValue;

    // EF Core parameterless constructor
    private RefreshToken() : base() { }

    public RefreshToken(Guid id, Guid userId, string tokenHash, DateTime expiresAtUtc) : base()
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("RefreshToken identifier cannot be empty.");
        }

        if (userId == Guid.Empty)
        {
            throw new IdentityDomainException("RefreshToken user identifier cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new IdentityDomainException("RefreshToken token hash cannot be empty.");
        }

        if (expiresAtUtc <= DateTime.UtcNow)
        {
            throw new IdentityDomainException("RefreshToken cannot be expired upon creation.");
        }

        Id = id;
        UserId = userId;
        TokenHash = tokenHash.Trim();
        ExpiresAtUtc = expiresAtUtc;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public static RefreshToken Create(Guid id, Guid userId, string tokenHash, DateTime expiresAtUtc)
    {
        return new RefreshToken(id, userId, tokenHash, expiresAtUtc);
    }

    public void Revoke(DateTime revokedAtUtc, Guid? replacedByTokenId = null)
    {
        if (RevokedAtUtc.HasValue)
        {
            throw new IdentityDomainException("Refresh token is already revoked.");
        }

        if (replacedByTokenId.HasValue && replacedByTokenId.Value == Guid.Empty)
        {
            throw new IdentityDomainException("Replacement token identifier cannot be empty.");
        }

        RevokedAtUtc = revokedAtUtc;
        ReplacedByTokenId = replacedByTokenId;
    }
}

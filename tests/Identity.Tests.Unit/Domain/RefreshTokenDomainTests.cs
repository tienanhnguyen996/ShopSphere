using FluentAssertions;
using ShopSphere.Services.Identity.Domain;
using ShopSphere.Services.Identity.Domain.Exceptions;
using Xunit;

namespace ShopSphere.Services.Identity.Tests.Unit.Domain;

public class RefreshTokenDomainTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldCreateActiveRefreshToken()
    {
        // Arrange
        var tokenId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tokenHash = "secure_token_hash_abc123";
        var expiresAtUtc = DateTime.UtcNow.AddDays(7);

        // Act
        var token = RefreshToken.Create(tokenId, userId, tokenHash, expiresAtUtc);

        // Assert
        token.Should().NotBeNull();
        token.Id.Should().Be(tokenId);
        token.UserId.Should().Be(userId);
        token.TokenHash.Should().Be(tokenHash);
        token.ExpiresAtUtc.Should().Be(expiresAtUtc);
        token.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        token.RevokedAtUtc.Should().BeNull();
        token.ReplacedByTokenId.Should().BeNull();
        token.IsActive.Should().BeTrue();
        token.IsExpired.Should().BeFalse();
        token.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public void Create_WithEmptyId_ShouldThrowIdentityDomainException()
    {
        // Act
        var act = () => RefreshToken.Create(Guid.Empty, Guid.NewGuid(), "hash", DateTime.UtcNow.AddDays(1));

        // Assert
        act.Should().Throw<IdentityDomainException>()
            .WithMessage("*identifier*");
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldThrowIdentityDomainException()
    {
        // Act
        var act = () => RefreshToken.Create(Guid.NewGuid(), Guid.Empty, "hash", DateTime.UtcNow.AddDays(1));

        // Assert
        act.Should().Throw<IdentityDomainException>()
            .WithMessage("*user*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyTokenHash_ShouldThrowIdentityDomainException(string? invalidHash)
    {
        // Act
        var act = () => RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), invalidHash!, DateTime.UtcNow.AddDays(1));

        // Assert
        act.Should().Throw<IdentityDomainException>()
            .WithMessage("*token hash*");
    }

    [Fact]
    public void Create_WithPastExpiration_ShouldThrowIdentityDomainException()
    {
        // Act
        var act = () => RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", DateTime.UtcNow.AddMinutes(-5));

        // Assert
        act.Should().Throw<IdentityDomainException>()
            .WithMessage("*expire*");
    }

    [Fact]
    public void Revoke_WhenActive_ShouldSetRevokedAtAndReplacedByTokenIdAndDeactivate()
    {
        // Arrange
        var token = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", DateTime.UtcNow.AddDays(7));
        var revokedAt = DateTime.UtcNow;
        var replacementId = Guid.NewGuid();

        // Act
        token.Revoke(revokedAt, replacementId);

        // Assert
        token.RevokedAtUtc.Should().Be(revokedAt);
        token.ReplacedByTokenId.Should().Be(replacementId);
        token.IsRevoked.Should().BeTrue();
        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Revoke_WithEmptyReplacementTokenId_ShouldThrowIdentityDomainException()
    {
        // Arrange
        var token = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", DateTime.UtcNow.AddDays(7));

        // Act
        var act = () => token.Revoke(DateTime.UtcNow, Guid.Empty);

        // Assert
        act.Should().Throw<IdentityDomainException>()
            .WithMessage("*replacement*");
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_ShouldThrowIdentityDomainException()
    {
        // Arrange
        var token = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", DateTime.UtcNow.AddDays(7));
        token.Revoke(DateTime.UtcNow);

        // Act
        var act = () => token.Revoke(DateTime.UtcNow);

        // Assert
        act.Should().Throw<IdentityDomainException>()
            .WithMessage("*already revoked*");
    }

    [Fact]
    public void IsActive_WhenExpired_ShouldReturnFalse()
    {
        // Arrange
        var token = RefreshToken.Create(Guid.NewGuid(), Guid.NewGuid(), "hash", DateTime.UtcNow.AddSeconds(1));
        Thread.Sleep(1100);

        // Assert
        token.IsExpired.Should().BeTrue();
        token.IsActive.Should().BeFalse();
    }
}

using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Identity.Application.Common.Interfaces;
using ShopSphere.Services.Identity.Application.Common.Security;
using ShopSphere.Services.Identity.Application.Contracts;
using ShopSphere.Services.Identity.Domain;

namespace ShopSphere.Services.Identity.Application.Auth.Commands.RefreshToken;

public class RefreshTokenCommandHandler
{
    private readonly IIdentityDbContext _dbContext;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IValidator<RefreshTokenCommand> _validator;

    public RefreshTokenCommandHandler(
        IIdentityDbContext dbContext,
        IJwtTokenGenerator jwtTokenGenerator,
        IValidator<RefreshTokenCommand> validator)
    {
        _dbContext = dbContext;
        _jwtTokenGenerator = jwtTokenGenerator;
        _validator = validator;
    }

    public async Task<Result<TokenResponse>> HandleAsync(RefreshTokenCommand command, CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
            return Result.Failure<TokenResponse>(Error.Validation("Auth.ValidationFailed", errors));
        }

        var tokenHash = TokenSecurityHelper.HashRefreshToken(command.RefreshToken);
        var token = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

        if (token is null)
        {
            return Result.Failure<TokenResponse>(Error.Unauthorized(
                "Auth.InvalidToken",
                "Invalid refresh token."));
        }

        if (token.IsRevoked)
        {
            // Token reuse / replay detected: invalidate entire active token family for this user
            var activeTokens = await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == token.UserId && rt.RevokedAtUtc == null)
                .ToListAsync(ct);

            foreach (var activeToken in activeTokens)
            {
                activeToken.Revoke(DateTime.UtcNow);
            }

            await _dbContext.SaveChangesAsync(ct);

            return Result.Failure<TokenResponse>(Error.Unauthorized(
                "Auth.TokenRevoked",
                "Refresh token has been revoked."));
        }

        if (token.IsExpired)
        {
            return Result.Failure<TokenResponse>(Error.Unauthorized(
                "Auth.TokenExpired",
                "Refresh token has expired."));
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == token.UserId, ct);

        if (user is null || !user.IsActive)
        {
            return Result.Failure<TokenResponse>(Error.Unauthorized(
                "Auth.UserInactive",
                "User account is inactive."));
        }

        // Single-use rotation
        var newRawToken = TokenSecurityHelper.GenerateRefreshToken();
        var newTokenHash = TokenSecurityHelper.HashRefreshToken(newRawToken);
        var newToken = Domain.RefreshToken.Create(
            Guid.NewGuid(),
            user.Id,
            newTokenHash,
            DateTime.UtcNow.AddDays(14));

        token.Revoke(DateTime.UtcNow, newToken.Id);
        _dbContext.RefreshTokens.Add(newToken);

        var (roleNames, permissions) = await ResolveRolesAndPermissionsAsync(user, ct);
        var accessToken = _jwtTokenGenerator.GenerateToken(user, roleNames, permissions);

        await _dbContext.SaveChangesAsync(ct);

        return Result.Success(new TokenResponse(
            accessToken,
            newRawToken,
            DateTime.UtcNow.AddMinutes(15)));
    }

    private async Task<(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)> ResolveRolesAndPermissionsAsync(User user, CancellationToken ct)
    {
        var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();
        if (roleIds.Count == 0)
        {
            roleIds = await _dbContext.UserRoles
                .Where(ur => ur.UserId == user.Id)
                .Select(ur => ur.RoleId)
                .ToListAsync(ct);
        }

        if (roleIds.Count == 0)
        {
            return (Array.Empty<string>(), Array.Empty<string>());
        }

        var roles = await _dbContext.Roles
            .Where(r => roleIds.Contains(r.Id))
            .ToListAsync(ct);
        var roleNames = roles.Select(r => r.Name).Distinct().ToList();

        var permissionIds = await _dbContext.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.PermissionId)
            .Distinct()
            .ToListAsync(ct);

        var permissions = await _dbContext.Permissions
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => p.Code)
            .Distinct()
            .ToListAsync(ct);

        return (roleNames, permissions);
    }
}

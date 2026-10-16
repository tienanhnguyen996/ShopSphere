using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Identity.Application.Common.Interfaces;
using ShopSphere.Services.Identity.Application.Common.Security;
using ShopSphere.Services.Identity.Application.Contracts;
using ShopSphere.Services.Identity.Domain;

namespace ShopSphere.Services.Identity.Application.Auth.Commands.Login;

public class LoginCommandHandler
{
    private readonly IIdentityDbContext _dbContext;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IValidator<LoginCommand> _validator;

    public LoginCommandHandler(
        IIdentityDbContext dbContext,
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordHashService passwordHashService,
        IValidator<LoginCommand> validator)
    {
        _dbContext = dbContext;
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHashService = passwordHashService;
        _validator = validator;
    }

    public async Task<Result<TokenResponse>> HandleAsync(LoginCommand command, CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
            return Result.Failure<TokenResponse>(Error.Validation("Auth.ValidationFailed", errors));
        }

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user is null)
        {
            return Result.Failure<TokenResponse>(Error.Unauthorized(
                "Auth.InvalidCredentials",
                "Invalid email or password."));
        }

        if (!user.IsActive)
        {
            return Result.Failure<TokenResponse>(Error.Unauthorized(
                "Auth.UserInactive",
                "User account is inactive."));
        }

        var isPasswordValid = _passwordHashService.VerifyPassword(command.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            return Result.Failure<TokenResponse>(Error.Unauthorized(
                "Auth.InvalidCredentials",
                "Invalid email or password."));
        }

        var (roleNames, permissions) = await ResolveRolesAndPermissionsAsync(user, ct);
        var accessToken = _jwtTokenGenerator.GenerateToken(user, roleNames, permissions);

        var rawRefreshToken = TokenSecurityHelper.GenerateRefreshToken();
        var tokenHash = TokenSecurityHelper.HashRefreshToken(rawRefreshToken);
        var refreshToken = Domain.RefreshToken.Create(Guid.NewGuid(), user.Id, tokenHash, DateTime.UtcNow.AddDays(14));
        _dbContext.RefreshTokens.Add(refreshToken);

        await _dbContext.SaveChangesAsync(ct);

        return Result.Success(new TokenResponse(
            accessToken,
            rawRefreshToken,
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

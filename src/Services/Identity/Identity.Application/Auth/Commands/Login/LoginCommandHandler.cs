using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Identity.Application.Common.Interfaces;
using ShopSphere.Services.Identity.Application.Common.Security;
using ShopSphere.Services.Identity.Application.Contracts;

namespace ShopSphere.Services.Identity.Application.Auth.Commands.Login;

public class LoginCommandHandler
{
    private readonly IIdentityDbContext _dbContext;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IUserRolePermissionResolver _rolePermissionResolver;
    private readonly IValidator<LoginCommand> _validator;

    public LoginCommandHandler(
        IIdentityDbContext dbContext,
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordHashService passwordHashService,
        IUserRolePermissionResolver rolePermissionResolver,
        IValidator<LoginCommand> validator)
    {
        _dbContext = dbContext;
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHashService = passwordHashService;
        _rolePermissionResolver = rolePermissionResolver;
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

        var (roleNames, permissions) = await _rolePermissionResolver.ResolveAsync(user, ct);
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
}

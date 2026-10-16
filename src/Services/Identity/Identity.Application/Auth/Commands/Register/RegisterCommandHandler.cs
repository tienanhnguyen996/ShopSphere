using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.BuildingBlocks.Messaging.Serialization;
using ShopSphere.BuildingBlocks.Outbox;
using ShopSphere.Services.Identity.Application.Common.Interfaces;
using ShopSphere.Services.Identity.Application.Common.Security;
using ShopSphere.Services.Identity.Application.Contracts;
using ShopSphere.Services.Identity.Domain;

namespace ShopSphere.Services.Identity.Application.Auth.Commands.Register;

public class RegisterCommandHandler
{
    private readonly IIdentityDbContext _dbContext;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IValidator<RegisterCommand> _validator;

    public RegisterCommandHandler(
        IIdentityDbContext dbContext,
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordHashService passwordHashService,
        IValidator<RegisterCommand> validator)
    {
        _dbContext = dbContext;
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHashService = passwordHashService;
        _validator = validator;
    }

    public async Task<Result<TokenResponse>> HandleAsync(RegisterCommand command, CancellationToken ct = default)
    {
        var validationResult = await _validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
            return Result.Failure<TokenResponse>(Error.Validation("User.ValidationFailed", errors));
        }

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();
        var exists = await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, ct);
        if (exists)
        {
            return Result.Failure<TokenResponse>(Error.Conflict(
                "User.DuplicateEmail",
                $"User with email '{command.Email}' already exists."));
        }

        var passwordHash = _passwordHashService.HashPassword(command.Password);
        var user = User.Create(Guid.NewGuid(), normalizedEmail, passwordHash, command.FullName);

        var customerRole = await _dbContext.Roles.FirstOrDefaultAsync(r => r.Name == "Customer", ct);
        if (customerRole is not null)
        {
            user.AssignRole(customerRole.Id);
            _dbContext.UserRoles.Add(new UserRole(user.Id, customerRole.Id));
        }

        _dbContext.Users.Add(user);

        var userRegisteredEvent = new UserRegistered(user.Id, user.Email, user.FullName, DateTime.UtcNow);
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = typeof(UserRegistered).FullName ?? nameof(UserRegistered),
            Content = ShopSphereJsonDefaults.Serialize(userRegisteredEvent),
            OccurredAtUtc = userRegisteredEvent.OccurredAtUtc
        };
        _dbContext.OutboxMessages.Add(outboxMessage);

        var rawRefreshToken = TokenSecurityHelper.GenerateRefreshToken();
        var tokenHash = TokenSecurityHelper.HashRefreshToken(rawRefreshToken);
        var refreshToken = Domain.RefreshToken.Create(Guid.NewGuid(), user.Id, tokenHash, DateTime.UtcNow.AddDays(14));
        _dbContext.RefreshTokens.Add(refreshToken);

        await _dbContext.SaveChangesAsync(ct);

        var (roleNames, permissions) = await ResolveRolesAndPermissionsAsync(user, ct);
        var accessToken = _jwtTokenGenerator.GenerateToken(user, roleNames, permissions);

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

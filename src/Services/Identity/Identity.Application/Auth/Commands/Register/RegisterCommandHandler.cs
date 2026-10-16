using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
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
    private readonly IUserRolePermissionResolver _rolePermissionResolver;
    private readonly IValidator<RegisterCommand> _validator;

    public RegisterCommandHandler(
        IIdentityDbContext dbContext,
        IJwtTokenGenerator jwtTokenGenerator,
        IPasswordHashService passwordHashService,
        IUserRolePermissionResolver rolePermissionResolver,
        IValidator<RegisterCommand> validator)
    {
        _dbContext = dbContext;
        _jwtTokenGenerator = jwtTokenGenerator;
        _passwordHashService = passwordHashService;
        _rolePermissionResolver = rolePermissionResolver;
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
        }

        _dbContext.Users.Add(user);

        var userRegisteredEvent = new UserRegistered(user.Id, user.Email, user.FullName, DateTime.UtcNow);
        var envelope = MessageEnvelope<UserRegistered>.Create(userRegisteredEvent, user.Id.ToString());
        var outboxMessage = new OutboxMessage
        {
            Id = envelope.MessageId,
            Type = typeof(MessageEnvelope<UserRegistered>).FullName ?? nameof(MessageEnvelope<UserRegistered>),
            Content = ShopSphereJsonDefaults.Serialize(envelope),
            OccurredAtUtc = envelope.OccurredAtUtc
        };
        _dbContext.OutboxMessages.Add(outboxMessage);

        var rawRefreshToken = TokenSecurityHelper.GenerateRefreshToken();
        var tokenHash = TokenSecurityHelper.HashRefreshToken(rawRefreshToken);
        var refreshToken = Domain.RefreshToken.Create(Guid.NewGuid(), user.Id, tokenHash, DateTime.UtcNow.AddDays(14));
        _dbContext.RefreshTokens.Add(refreshToken);

        await _dbContext.SaveChangesAsync(ct);

        var (roleNames, permissions) = await _rolePermissionResolver.ResolveAsync(user, ct);
        var accessToken = _jwtTokenGenerator.GenerateToken(user, roleNames, permissions);

        return Result.Success(new TokenResponse(
            accessToken,
            rawRefreshToken,
            DateTime.UtcNow.AddMinutes(15)));
    }
}

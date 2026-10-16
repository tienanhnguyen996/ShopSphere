using Microsoft.EntityFrameworkCore;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Identity.Application.Common.Interfaces;
using ShopSphere.Services.Identity.Application.Common.Security;

namespace ShopSphere.Services.Identity.Application.Auth.Commands.RevokeToken;

public class RevokeTokenCommandHandler
{
    private readonly IIdentityDbContext _dbContext;

    public RevokeTokenCommandHandler(IIdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> HandleAsync(RevokeTokenCommand command, CancellationToken ct = default)
    {
        if (command.RevokeAll)
        {
            var activeTokens = await _dbContext.RefreshTokens
                .Where(rt => rt.UserId == command.UserId && rt.RevokedAtUtc == null)
                .ToListAsync(ct);

            foreach (var token in activeTokens)
            {
                token.Revoke(DateTime.UtcNow);
            }

            await _dbContext.SaveChangesAsync(ct);
            return Result.Success();
        }

        if (!string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            var tokenHash = TokenSecurityHelper.HashRefreshToken(command.RefreshToken);
            var token = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.UserId == command.UserId && rt.TokenHash == tokenHash, ct);

            if (token is not null && !token.IsRevoked)
            {
                token.Revoke(DateTime.UtcNow);
                await _dbContext.SaveChangesAsync(ct);
            }
        }

        return Result.Success();
    }
}

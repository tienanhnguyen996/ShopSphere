namespace ShopSphere.Services.Identity.Application.Auth.Commands.RevokeToken;

public sealed record RevokeTokenCommand(
    Guid UserId,
    string? RefreshToken = null,
    bool RevokeAll = false);

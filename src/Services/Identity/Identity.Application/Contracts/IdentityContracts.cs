namespace ShopSphere.Services.Identity.Application.Contracts;

public record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    string TokenType = "Bearer");

public record UserProfileDto(
    Guid Id,
    string Email,
    string FullName,
    bool IsActive,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    DateTime CreatedAtUtc);

public record RoleDto(
    Guid Id,
    string Name,
    string Description,
    IReadOnlyList<string> Permissions);

public record PermissionDto(
    Guid Id,
    string Code,
    string Description);

public record UserRegistered(
    Guid UserId,
    string Email,
    string FullName,
    DateTime OccurredAtUtc);

namespace ShopSphere.Services.Identity.Application.Auth.Commands.Register;

public sealed record RegisterCommand(
    string Email,
    string Password,
    string FullName);

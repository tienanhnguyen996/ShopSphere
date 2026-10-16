namespace ShopSphere.Services.Identity.Application.Auth.Commands.Login;

public sealed record LoginCommand(
    string Email,
    string Password);

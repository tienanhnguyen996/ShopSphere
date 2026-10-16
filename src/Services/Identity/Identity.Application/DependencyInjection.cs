using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.Services.Identity.Application.Auth.Commands.Login;
using ShopSphere.Services.Identity.Application.Auth.Commands.RefreshToken;
using ShopSphere.Services.Identity.Application.Auth.Commands.Register;
using ShopSphere.Services.Identity.Application.Auth.Commands.RevokeToken;
using ShopSphere.Services.Identity.Application.Auth.Queries.GetCurrentUser;
using ShopSphere.Services.Identity.Application.Roles.Queries.GetRoles;
using ShopSphere.Services.Identity.Application.Users.Commands.AssignRole;
using ShopSphere.Services.Identity.Application.Users.Commands.RemoveRole;
using ShopSphere.Services.Identity.Application.Users.Queries.GetUsers;

namespace ShopSphere.Services.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterCommandValidator>();

        services.AddScoped<RegisterCommandHandler>();
        services.AddScoped<LoginCommandHandler>();
        services.AddScoped<RefreshTokenCommandHandler>();
        services.AddScoped<RevokeTokenCommandHandler>();
        services.AddScoped<GetCurrentUserQueryHandler>();
        services.AddScoped<AssignRoleCommandHandler>();
        services.AddScoped<RemoveRoleCommandHandler>();
        services.AddScoped<GetUsersQueryHandler>();
        services.AddScoped<GetRolesQueryHandler>();

        return services;
    }
}

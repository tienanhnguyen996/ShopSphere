using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ShopSphere.BuildingBlocks.Security;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddShopSphereAuthorization(
        this IServiceCollection services,
        Action<AuthorizationOptions>? configure = null)
    {
        services.AddAuthorization(options =>
        {
            foreach (var permission in ShopSpherePermissions.All)
            {
                options.AddPolicy(permission, policy =>
                {
                    if (permission == ShopSpherePermissions.SuperAdmin)
                    {
                        policy.RequireClaim("permission", ShopSpherePermissions.SuperAdmin);
                    }
                    else
                    {
                        policy.RequireClaim("permission", permission, ShopSpherePermissions.SuperAdmin);
                    }
                });
            }

            configure?.Invoke(options);
        });

        return services;
    }

    public static TBuilder RequirePermission<TBuilder>(
        this TBuilder builder,
        string permission) where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        return builder.RequireAuthorization(permission);
    }

    public static TBuilder RequirePermissions<TBuilder>(
        this TBuilder builder,
        params string[] permissions) where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(permissions);

        return builder.RequireAuthorization(permissions);
    }
}


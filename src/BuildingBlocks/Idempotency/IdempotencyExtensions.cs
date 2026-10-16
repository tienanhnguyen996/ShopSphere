using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ShopSphere.BuildingBlocks.Idempotency;

public static class IdempotencyExtensions
{
    public static IServiceCollection AddShopSphereIdempotency(this IServiceCollection services)
    {
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        return services;
    }

    public static RouteHandlerBuilder WithIdempotency(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter<IdempotentEndpointFilter>();
    }
}

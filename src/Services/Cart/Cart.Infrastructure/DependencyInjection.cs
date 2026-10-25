using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.Services.Cart.Application.Common.Interfaces;
using ShopSphere.Services.Cart.Infrastructure.Clients;
using ShopSphere.Services.Cart.Infrastructure.Repositories;

namespace ShopSphere.Services.Cart.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCartInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                var configOptions = StackExchange.Redis.ConfigurationOptions.Parse(redisConnection);
                configOptions.CertificateValidation += (sender, cert, chain, errors) => true;
                options.ConfigurationOptions = configOptions;
                options.InstanceName = "ShopSphere_";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.AddScoped<ICartRepository, RedisCartRepository>();

        var catalogBaseUrl = configuration["Services:Catalog:BaseUrl"] ?? "http://localhost:5001";
        services.AddHttpClient<ICatalogClient, CatalogHttpClient>(client =>
        {
            client.BaseAddress = new Uri(catalogBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace ShopSphere.BuildingBlocks.Resilience;

public static class ResilienceExtensions
{
    public static IHttpClientBuilder AddResilientHttpClient<TClient, TImplementation>(
        this IServiceCollection services,
        Action<HttpClient>? configureClient = null)
        where TClient : class
        where TImplementation : class, TClient
    {
        var builder = configureClient is not null
            ? services.AddHttpClient<TClient, TImplementation>(configureClient)
            : services.AddHttpClient<TClient, TImplementation>();

        builder.AddStandardResilienceHandler(ConfigureShopSphereResilience);
        return builder;
    }

    public static IHttpClientBuilder AddResilientHttpClient(
        this IServiceCollection services,
        string clientName,
        Action<HttpClient>? configureClient = null)
    {
        var builder = configureClient is not null
            ? services.AddHttpClient(clientName, configureClient)
            : services.AddHttpClient(clientName);

        builder.AddStandardResilienceHandler(ConfigureShopSphereResilience);
        return builder;
    }

    public static void ConfigureShopSphereResilience(HttpStandardResilienceOptions options)
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);

        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);

        options.Retry.MaxRetryAttempts = 3;
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;

        options.Retry.ShouldHandle = args =>
        {
            var request = args.Outcome.Result?.RequestMessage;
            if (request is not null)
            {
                var isIdempotentMethod = request.Method == HttpMethod.Get ||
                                         request.Method == HttpMethod.Put ||
                                         request.Method == HttpMethod.Delete ||
                                         request.Method == HttpMethod.Head ||
                                         request.Method == HttpMethod.Options;

                var hasIdempotencyKey = request.Headers.Contains("Idempotency-Key");

                if (!isIdempotentMethod && !hasIdempotencyKey)
                {
                    return ValueTask.FromResult(false);
                }
            }

            if (args.Outcome.Exception is not null)
            {
                return ValueTask.FromResult(true);
            }

            if (args.Outcome.Result is not null)
            {
                var status = (int)args.Outcome.Result.StatusCode;
                var isTransientError = status == 408 || status >= 500;
                return ValueTask.FromResult(isTransientError);
            }

            return ValueTask.FromResult(false);
        };
    }
}

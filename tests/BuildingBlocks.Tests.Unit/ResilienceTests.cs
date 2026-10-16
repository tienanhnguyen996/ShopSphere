using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Retry;
using ShopSphere.BuildingBlocks.Resilience;
using Xunit;

namespace ShopSphere.BuildingBlocks.Tests.Unit;

public class ResilienceTests
{
    public interface ITestApiClient
    {
        HttpClient Client { get; }
    }

    public class TestApiClient : ITestApiClient
    {
        public TestApiClient(HttpClient client)
        {
            Client = client;
        }

        public HttpClient Client { get; }
    }

    [Fact]
    public void AddResilientHttpClient_RegistersClientWithResilienceHandler()
    {
        var services = new ServiceCollection();
        services.AddResilientHttpClient<ITestApiClient, TestApiClient>(client =>
        {
            client.BaseAddress = new Uri("http://localhost:5000");
        });

        var provider = services.BuildServiceProvider();
        var client = provider.GetService<ITestApiClient>();

        client.Should().NotBeNull();
        client!.Client.BaseAddress.Should().Be(new Uri("http://localhost:5000"));
    }

    [Fact]
    public void ConfigureShopSphereResilience_SetsCorrectTimeoutsAndCircuitBreaker()
    {
        var options = new HttpStandardResilienceOptions();
        ResilienceExtensions.ConfigureShopSphereResilience(options);

        options.TotalRequestTimeout.Timeout.Should().Be(TimeSpan.FromSeconds(5));
        options.AttemptTimeout.Timeout.Should().Be(TimeSpan.FromSeconds(5));

        options.CircuitBreaker.SamplingDuration.Should().Be(TimeSpan.FromSeconds(30));
        options.CircuitBreaker.FailureRatio.Should().Be(0.5);
        options.CircuitBreaker.BreakDuration.Should().Be(TimeSpan.FromSeconds(15));

        options.Retry.MaxRetryAttempts.Should().Be(3);
        options.Retry.BackoffType.Should().Be(DelayBackoffType.Exponential);
        options.Retry.UseJitter.Should().BeTrue();
    }

    [Fact]
    public async Task RetryPolicy_ShouldHandle_IdempotentVsNonIdempotent()
    {
        var options = new HttpStandardResilienceOptions();
        ResilienceExtensions.ConfigureShopSphereResilience(options);

        // 1. Non-idempotent POST without idempotency key -> MUST NOT retry
        var nonIdempotentReq = new HttpRequestMessage(HttpMethod.Post, "http://localhost/pay");
        var nonIdempotentResp = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            RequestMessage = nonIdempotentReq
        };
        var context1 = ResilienceContextPool.Shared.Get();
        var argsNonIdempotent = new RetryPredicateArguments<HttpResponseMessage>(
            context1,
            Polly.Outcome.FromResult(nonIdempotentResp),
            0);

        var shouldRetryNonIdempotent = await options.Retry.ShouldHandle(argsNonIdempotent);
        shouldRetryNonIdempotent.Should().BeFalse();
        ResilienceContextPool.Shared.Return(context1);

        // 2. GET request with 500 error -> MUST retry
        var getReq = new HttpRequestMessage(HttpMethod.Get, "http://localhost/catalog");
        var getResp = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            RequestMessage = getReq
        };
        var context2 = ResilienceContextPool.Shared.Get();
        var argsGet = new RetryPredicateArguments<HttpResponseMessage>(
            context2,
            Polly.Outcome.FromResult(getResp),
            0);

        var shouldRetryGet = await options.Retry.ShouldHandle(argsGet);
        shouldRetryGet.Should().BeTrue();
        ResilienceContextPool.Shared.Return(context2);

        // 3. POST request WITH Idempotency-Key -> MUST retry
        var idempotentPostReq = new HttpRequestMessage(HttpMethod.Post, "http://localhost/pay");
        idempotentPostReq.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var idempotentPostResp = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            RequestMessage = idempotentPostReq
        };
        var context3 = ResilienceContextPool.Shared.Get();
        var argsIdempotentPost = new RetryPredicateArguments<HttpResponseMessage>(
            context3,
            Polly.Outcome.FromResult(idempotentPostResp),
            0);

        var shouldRetryIdempotentPost = await options.Retry.ShouldHandle(argsIdempotentPost);
        shouldRetryIdempotentPost.Should().BeTrue();
        ResilienceContextPool.Shared.Return(context3);
    }
}

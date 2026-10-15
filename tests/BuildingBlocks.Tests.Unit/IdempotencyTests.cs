using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.BuildingBlocks.Idempotency;
using Xunit;

namespace ShopSphere.BuildingBlocks.Tests.Unit;

public class IdempotencyTests
{
    [Fact]
    public void IdempotencyFingerprint_ProducesConsistentSha256()
    {
        var fp1 = IdempotencyFingerprint.Compute("POST", "/api/orders", "{\"amount\":100}");
        var fp2 = IdempotencyFingerprint.Compute("post", "/api/orders/", "{\"amount\":100}");
        var fp3 = IdempotencyFingerprint.Compute("POST", "/api/orders", "{\"amount\":200}");

        fp1.Should().NotBeNullOrWhiteSpace();
        fp1.Length.Should().Be(64); // SHA-256 hex string
        fp1.Should().Be(fp2);
        fp1.Should().NotBe(fp3);
    }

    [Fact]
    public async Task InMemoryStore_SaveAndRetrieve_WithExpiry()
    {
        var store = new InMemoryIdempotencyStore();
        var record = new IdempotencyRecord
        {
            Key = "key-1",
            RequestFingerprint = "fp-1",
            ResponseStatusCode = 200,
            ResponseBody = "{\"ok\":true}",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5)
        };

        await store.SaveAsync(record);
        var retrieved = await store.GetAsync("key-1");

        retrieved.Should().NotBeNull();
        retrieved!.Key.Should().Be("key-1");
        retrieved.ResponseBody.Should().Be("{\"ok\":true}");

        var expiredRecord = new IdempotencyRecord
        {
            Key = "key-expired",
            RequestFingerprint = "fp-exp",
            ResponseStatusCode = 200,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-5)
        };
        await store.SaveAsync(expiredRecord);
        var expiredRetrieved = await store.GetAsync("key-expired");

        expiredRetrieved.Should().BeNull();
    }

    [Fact]
    public async Task IdempotentEndpointFilter_SameKeySamePayload_ReplaysCachedResponse()
    {
        var services = new ServiceCollection();
        services.AddShopSphereIdempotency();
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider
        };
        httpContext.Request.Headers[IdempotentEndpointFilter.HeaderName] = "test-key-100";
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = "/api/test";

        var filter = new IdempotentEndpointFilter();
        var invocationContext = new DefaultEndpointFilterInvocationContext(httpContext);

        int executionCount = 0;
        EndpointFilterDelegate next = _ =>
        {
            executionCount++;
            return ValueTask.FromResult<object?>(TypedResults.Ok(new { success = true, count = executionCount }));
        };

        // First execution
        var result1 = await filter.InvokeAsync(invocationContext, next);
        result1.Should().BeAssignableTo<IStatusCodeHttpResult>();
        executionCount.Should().Be(1);

        // Second execution with same key and request parameters
        var result2 = await filter.InvokeAsync(invocationContext, next);
        executionCount.Should().Be(1); // Cached, delegate not executed again
        result2.Should().NotBeNull();
    }

    [Fact]
    public async Task IdempotentEndpointFilter_SameKeyDifferentPayload_ReturnsConflict()
    {
        var services = new ServiceCollection();
        services.AddShopSphereIdempotency();
        var provider = services.BuildServiceProvider();

        var httpContext1 = new DefaultHttpContext { RequestServices = provider };
        httpContext1.Request.Headers[IdempotentEndpointFilter.HeaderName] = "conflict-key-1";
        httpContext1.Request.Method = "POST";
        httpContext1.Request.Path = "/api/test1";

        var filter = new IdempotentEndpointFilter();
        var ctx1 = new DefaultEndpointFilterInvocationContext(httpContext1);

        EndpointFilterDelegate next1 = _ => ValueTask.FromResult<object?>(TypedResults.Ok("payload1"));
        await filter.InvokeAsync(ctx1, next1);

        // Different request path with same key
        var httpContext2 = new DefaultHttpContext { RequestServices = provider };
        httpContext2.Request.Headers[IdempotentEndpointFilter.HeaderName] = "conflict-key-1";
        httpContext2.Request.Method = "POST";
        httpContext2.Request.Path = "/api/test2";

        var ctx2 = new DefaultEndpointFilterInvocationContext(httpContext2);
        EndpointFilterDelegate next2 = _ => ValueTask.FromResult<object?>(TypedResults.Ok("payload2"));

        var conflictResult = await filter.InvokeAsync(ctx2, next2);
        conflictResult.Should().NotBeNull();
        conflictResult!.GetType().Name.Should().Contain("Conflict");
    }
}

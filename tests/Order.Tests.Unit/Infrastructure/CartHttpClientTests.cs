using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ShopSphere.Services.Order.Application.Common.Interfaces;
using ShopSphere.Services.Order.Infrastructure.Clients;
using Xunit;

namespace ShopSphere.Services.Order.Tests.Unit.Infrastructure;

public class CartHttpClientTests
{
    [Fact]
    public async Task GetCheckoutQuoteAsync_WhenCartServiceReturns200_ShouldReturnSuccessQuote()
    {
        // Arrange
        var quote = new CartCheckoutQuoteDto(
            "cust-1",
            [new CartQuoteItemDto("SKU-1", "Laptop", 1, 999.99m, 999.99m, "USD", 2, true)],
            999.99m,
            "USD",
            new Dictionary<string, int> { ["SKU-1"] = 2 },
            true,
            []);

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(quote), Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002") };
        var client = new CartHttpClient(httpClient, NullLogger<CartHttpClient>.Instance);

        // Act
        var result = await client.GetCheckoutQuoteAsync("cust-1");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CustomerId.Should().Be("cust-1");
        result.Value.Subtotal.Should().Be(999.99m);
        result.Value.Items.Should().ContainSingle(i => i.Sku == "SKU-1" && i.PriceVersion == 2);
    }

    [Fact]
    public async Task GetCheckoutQuoteAsync_WhenCartServiceUnavailable_ShouldReturnFailure()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002") };
        var client = new CartHttpClient(httpClient, NullLogger<CartHttpClient>.Instance);

        // Act
        var result = await client.GetCheckoutQuoteAsync("cust-1");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("Unavailable");
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}

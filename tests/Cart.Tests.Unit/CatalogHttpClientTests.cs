using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Cart.Application.Common.Interfaces;
using ShopSphere.Services.Cart.Infrastructure.Clients;
using Xunit;

namespace ShopSphere.Services.Cart.Tests.Unit;

public class CatalogHttpClientTests
{
    private readonly Mock<ILogger<CatalogHttpClient>> _loggerMock = new();

    private class TestHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public TestHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory(request));
        }
    }

    private class ThrowingHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw _exception;
        }
    }

    [Fact]
    public async Task GetProductBySkuAsync_WhenResponseIs200_ShouldReturnProduct()
    {
        // Arrange
        var productDto = new CatalogProductDto(
            Guid.NewGuid(), "Logitech Mouse", "Wireless mouse", "MOU-LOGI-MX3", 99.99m, "USD", "Active", 2);

        var handler = new TestHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(productDto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                System.Text.Encoding.UTF8,
                "application/json")
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://catalog-service") };
        var client = new CatalogHttpClient(httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetProductBySkuAsync("MOU-LOGI-MX3");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Sku.Should().Be("MOU-LOGI-MX3");
        result.Value.Price.Should().Be(99.99m);
        result.Value.PriceVersion.Should().Be(2);
    }

    [Fact]
    public async Task GetProductBySkuAsync_WhenResponseIs404_ShouldReturnNotFoundError()
    {
        // Arrange
        var handler = new TestHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://catalog-service") };
        var client = new CatalogHttpClient(httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetProductBySkuAsync("UNKNOWN-SKU");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Catalog.ProductNotFound");
    }

    [Fact]
    public async Task GetProductBySkuAsync_WhenResponseIs500_ShouldReturnCatalogUnavailable()
    {
        // Arrange
        var handler = new TestHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://catalog-service") };
        var client = new CatalogHttpClient(httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetProductBySkuAsync("KB-001");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Failure);
        result.Error.Code.Should().Be("Cart.CatalogUnavailable");
    }

    [Fact]
    public async Task GetProductBySkuAsync_WhenHttpThrowsHttpRequestException_ShouldReturnCatalogUnavailable()
    {
        // Arrange
        var handler = new ThrowingHandler(new HttpRequestException("Connection refused"));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://catalog-service") };
        var client = new CatalogHttpClient(httpClient, _loggerMock.Object);

        // Act
        var result = await client.GetProductBySkuAsync("KB-001");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Failure);
        result.Error.Code.Should().Be("Cart.CatalogUnavailable");
    }
}

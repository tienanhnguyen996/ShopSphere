using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Cart.Application.Common.Interfaces;

namespace ShopSphere.Services.Cart.Infrastructure.Clients;

public class CatalogHttpClient : ICatalogClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CatalogHttpClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CatalogHttpClient(HttpClient httpClient, ILogger<CatalogHttpClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<CatalogProductDto>> GetProductBySkuAsync(string sku, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return Result.Failure<CatalogProductDto>(Error.Validation("Cart.InvalidSku", "SKU is required."));
        }

        var normalizedSku = Uri.EscapeDataString(sku.Trim().ToUpperInvariant());
        var endpoint = $"/api/v1/catalog/products/{normalizedSku}";

        try
        {
            var response = await _httpClient.GetAsync(endpoint, ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Result.Failure<CatalogProductDto>(
                    Error.NotFound("Catalog.ProductNotFound", $"Product with SKU '{sku}' was not found."));
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Catalog service returned non-success status code {StatusCode} for SKU {Sku}", response.StatusCode, sku);
                return Result.Failure<CatalogProductDto>(
                    Error.Failure("Cart.CatalogUnavailable", "Catalog service is currently unavailable."));
            }

            var product = await response.Content.ReadFromJsonAsync<CatalogProductDto>(JsonOptions, ct);
            if (product is null)
            {
                _logger.LogWarning("Catalog service returned empty payload for SKU {Sku}", sku);
                return Result.Failure<CatalogProductDto>(
                    Error.Failure("Cart.CatalogUnavailable", "Catalog service returned invalid response."));
            }

            return Result.Success(product);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failure contacting Catalog service for SKU {Sku}", sku);
            return Result.Failure<CatalogProductDto>(
                Error.Failure("Cart.CatalogUnavailable", "Catalog service is currently unreachable."));
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != ct)
        {
            _logger.LogError(ex, "Timeout contacting Catalog service for SKU {Sku}", sku);
            return Result.Failure<CatalogProductDto>(
                Error.Failure("Cart.CatalogUnavailable", "Catalog service request timed out."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error communicating with Catalog service for SKU {Sku}", sku);
            return Result.Failure<CatalogProductDto>(
                Error.Failure("Cart.CatalogUnavailable", "Error communicating with Catalog service."));
        }
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.Services.Order.Application.Common.Interfaces;

namespace ShopSphere.Services.Order.Infrastructure.Clients;

public class CartHttpClient : ICartClient
{
    public const string CustomerIdHeader = "X-Customer-Id";

    private readonly HttpClient _httpClient;
    private readonly ILogger<CartHttpClient> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public CartHttpClient(HttpClient httpClient, ILogger<CartHttpClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<CartCheckoutQuoteDto>> GetCheckoutQuoteAsync(string customerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return Result.Failure<CartCheckoutQuoteDto>(
                Error.Validation("Order.CustomerIdRequired", "Customer ID is required to fetch cart quote."));
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/cart/quote");
            request.Headers.Add(CustomerIdHeader, customerId.Trim());

            var response = await _httpClient.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return Result.Failure<CartCheckoutQuoteDto>(
                    Error.Validation("Order.CartQuoteInvalid", "Cart is empty or quote validation failed."));
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Cart service returned status {StatusCode} for customer {CustomerId}", response.StatusCode, customerId);
                return Result.Failure<CartCheckoutQuoteDto>(
                    Error.Failure("Order.CartServiceUnavailable", "Cart service is currently unavailable."));
            }

            var quote = await response.Content.ReadFromJsonAsync<CartCheckoutQuoteDto>(_jsonOptions, ct);
            if (quote is null)
            {
                return Result.Failure<CartCheckoutQuoteDto>(
                    Error.Failure("Order.CartServiceUnavailable", "Cart service returned an empty quote payload."));
            }

            return Result.Success(quote);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP failure contacting Cart service for customer {CustomerId}", customerId);
            return Result.Failure<CartCheckoutQuoteDto>(
                Error.Failure("Order.CartServiceUnavailable", "Cart service is unreachable."));
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != ct)
        {
            _logger.LogError(ex, "Timeout contacting Cart service for customer {CustomerId}", customerId);
            return Result.Failure<CartCheckoutQuoteDto>(
                Error.Failure("Order.CartServiceUnavailable", "Cart service request timed out."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error communicating with Cart service for customer {CustomerId}", customerId);
            return Result.Failure<CartCheckoutQuoteDto>(
                Error.Failure("Order.CartServiceUnavailable", "Error communicating with Cart service."));
        }
    }

    public async Task<Result> ClearCartAsync(string customerId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return Result.Failure(Error.Validation("Order.CustomerIdRequired", "Customer ID is required."));
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/cart");
            request.Headers.Add(CustomerIdHeader, customerId.Trim());

            var response = await _httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
            {
                return Result.Success();
            }

            return Result.Failure(Error.Failure("Order.ClearCartFailed", "Failed to clear customer cart."));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Non-fatal failure clearing cart after checkout for customer {CustomerId}", customerId);
            return Result.Failure(Error.Failure("Order.ClearCartFailed", "Cart clear request failed."));
        }
    }
}

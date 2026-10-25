using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShopSphere.Services.Payment.Application.Common.Interfaces;
using ShopSphere.Services.Payment.Infrastructure.Configuration;

namespace ShopSphere.Services.Payment.Infrastructure.Clients;

public class StripePaymentProviderClient : IPaymentProviderClient
{
    private readonly HttpClient _httpClient;
    private readonly StripeSettings _settings;
    private readonly ILogger<StripePaymentProviderClient> _logger;

    public StripePaymentProviderClient(
        HttpClient httpClient,
        IOptions<PaymentSettings> settings,
        ILogger<StripePaymentProviderClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value.Stripe ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
        }
    }

    public async Task<PaymentChargeResult> ChargeAsync(PaymentChargeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Calling Stripe PaymentIntents API for Order {OrderId}, Attempt {AttemptId}, Amount {Amount} {Currency}",
            request.OrderId,
            request.PaymentAttemptId,
            request.Amount,
            request.Currency);

        var amountInSmallestUnit = ToStripeAmount(request.Amount, request.Currency);

        var formParams = new Dictionary<string, string>
        {
            ["amount"] = amountInSmallestUnit.ToString(CultureInfo.InvariantCulture),
            ["currency"] = request.Currency.ToLowerInvariant(),
            ["payment_method"] = "pm_card_visa",
            ["confirm"] = "true",
            ["automatic_payment_methods[enabled]"] = "true",
            ["automatic_payment_methods[allow_redirects]"] = "never",
            ["metadata[order_id]"] = request.OrderId.ToString(),
            ["metadata[attempt_id]"] = request.PaymentAttemptId.ToString()
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "payment_intents")
        {
            Content = new FormUrlEncodedContent(formParams)
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        httpRequest.Headers.Add("Idempotency-Key", request.PaymentAttemptId.ToString());

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var jsonNode = JsonNode.Parse(content);
                var paymentIntentId = jsonNode?["id"]?.ToString();
                var status = jsonNode?["status"]?.ToString();

                if (status == "succeeded")
                {
                    _logger.LogInformation("Stripe charge succeeded for Attempt {AttemptId}: {IntentId}", request.PaymentAttemptId, paymentIntentId);
                    return new PaymentChargeResult(PaymentProviderStatus.Success, paymentIntentId, null);
                }

                _logger.LogWarning("Stripe charge status {Status} for Attempt {AttemptId}: {IntentId}", status, request.PaymentAttemptId, paymentIntentId);
                return new PaymentChargeResult(PaymentProviderStatus.Failed, paymentIntentId, $"Stripe charge did not succeed. Current status: {status}");
            }

            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout)
            {
                _logger.LogWarning("Stripe returned server error {StatusCode} for Attempt {AttemptId}. Mapping to Unknown.", response.StatusCode, request.PaymentAttemptId);
                return new PaymentChargeResult(PaymentProviderStatus.Unknown, null, $"Stripe API returned HTTP {(int)response.StatusCode}. Status is unknown.");
            }

            var parsedError = ExtractErrorMessage(content);
            _logger.LogInformation("Stripe charge declined or failed: {Error}", parsedError);
            return new PaymentChargeResult(PaymentProviderStatus.Failed, null, parsedError);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Stripe API call timed out for Attempt {AttemptId}. Mapping to Unknown.", request.PaymentAttemptId);
            return new PaymentChargeResult(PaymentProviderStatus.Unknown, null, "Stripe API call timed out; transaction status is unknown.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Stripe API network exception for Attempt {AttemptId}. Mapping to Unknown.", request.PaymentAttemptId);
            return new PaymentChargeResult(PaymentProviderStatus.Unknown, null, $"Stripe network failure: {ex.Message}");
        }
    }

    public async Task<PaymentStatusQueryResult> GetStatusAsync(PaymentStatusQueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.ProviderTransactionId))
        {
            return new PaymentStatusQueryResult(PaymentProviderStatus.Unknown, null, "No ProviderTransactionId provided for status query.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"payment_intents/{request.ProviderTransactionId}");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var jsonNode = JsonNode.Parse(content);
                var status = jsonNode?["status"]?.ToString();
                var intentId = jsonNode?["id"]?.ToString();

                if (status == "succeeded")
                {
                    return new PaymentStatusQueryResult(PaymentProviderStatus.Success, intentId, null);
                }

                if (status is "canceled" or "requires_payment_method")
                {
                    return new PaymentStatusQueryResult(PaymentProviderStatus.Failed, intentId, $"Payment status is {status}");
                }

                return new PaymentStatusQueryResult(PaymentProviderStatus.Unknown, intentId, $"Payment status is pending: {status}");
            }

            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout)
            {
                return new PaymentStatusQueryResult(PaymentProviderStatus.Unknown, request.ProviderTransactionId, $"Stripe query returned HTTP {(int)response.StatusCode}");
            }

            return new PaymentStatusQueryResult(PaymentProviderStatus.Failed, request.ProviderTransactionId, ExtractErrorMessage(content));
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException)
        {
            return new PaymentStatusQueryResult(PaymentProviderStatus.Unknown, request.ProviderTransactionId, $"Stripe query error: {ex.Message}");
        }
    }

    public async Task<PaymentRefundResult> RefundAsync(PaymentRefundRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var amountInSmallestUnit = ToStripeAmount(request.Amount, request.Currency);
        var formParams = new Dictionary<string, string>
        {
            ["payment_intent"] = request.ProviderTransactionId,
            ["amount"] = amountInSmallestUnit.ToString(CultureInfo.InvariantCulture),
            ["reason"] = "requested_by_customer"
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "refunds")
        {
            Content = new FormUrlEncodedContent(formParams)
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        httpRequest.Headers.Add("Idempotency-Key", $"ref_{request.PaymentAttemptId:N}");

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var jsonNode = JsonNode.Parse(content);
                var refundId = jsonNode?["id"]?.ToString();
                var status = jsonNode?["status"]?.ToString();

                if (status == "succeeded" || status == "pending")
                {
                    return new PaymentRefundResult(PaymentProviderStatus.Success, refundId, null);
                }

                return new PaymentRefundResult(PaymentProviderStatus.Failed, refundId, $"Refund status: {status}");
            }

            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout)
            {
                return new PaymentRefundResult(PaymentProviderStatus.Unknown, null, $"Stripe refund returned HTTP {(int)response.StatusCode}");
            }

            return new PaymentRefundResult(PaymentProviderStatus.Failed, null, ExtractErrorMessage(content));
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException)
        {
            return new PaymentRefundResult(PaymentProviderStatus.Unknown, null, $"Stripe refund error: {ex.Message}");
        }
    }

    private static long ToStripeAmount(decimal amount, string currency)
    {
        // Zero-decimal currencies like VND, JPY, KRW do not multiply by 100
        var isZeroDecimal = currency.Equals("VND", StringComparison.OrdinalIgnoreCase)
                         || currency.Equals("JPY", StringComparison.OrdinalIgnoreCase)
                         || currency.Equals("KRW", StringComparison.OrdinalIgnoreCase);

        return isZeroDecimal
            ? (long)Math.Round(amount, MidpointRounding.AwayFromZero)
            : (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
    }

    private static string ExtractErrorMessage(string jsonContent)
    {
        try
        {
            var node = JsonNode.Parse(jsonContent);
            var message = node?["error"]?["message"]?.ToString();
            if (!string.IsNullOrWhiteSpace(message))
            {
                return message;
            }
        }
        catch
        {
            // Ignore parse failure
        }

        return string.IsNullOrWhiteSpace(jsonContent) ? "Payment provider request failed." : jsonContent;
    }
}

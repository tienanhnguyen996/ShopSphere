using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using ShopSphere.Services.Payment.Application.Common.Interfaces;

namespace ShopSphere.Services.Payment.Infrastructure.Clients;

/// <summary>
/// Mock external payment provider client implementing Polly resilience and explicit timeout handling.
/// Timeout = PaymentProviderStatus.Unknown, NOT Failed.
/// </summary>
public class MockPaymentProviderClient : IPaymentProviderClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MockPaymentProviderClient> _logger;
    private static readonly ConcurrentDictionary<Guid, (PaymentProviderStatus Status, string? TransactionId)> _knownAttempts = new();

    public MockPaymentProviderClient(HttpClient httpClient, ILogger<MockPaymentProviderClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Test helper to configure or seed known attempt status for reconciliation tests.
    /// </summary>
    public static void SetAttemptStatus(Guid attemptId, PaymentProviderStatus status, string? transactionId = null)
    {
        _knownAttempts[attemptId] = (status, transactionId);
    }

    /// <summary>
    /// Test helper to clear known attempts.
    /// </summary>
    public static void ClearKnownAttempts()
    {
        _knownAttempts.Clear();
    }

    public async Task<PaymentChargeResult> ChargeAsync(PaymentChargeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Calling external payment provider for Order {OrderId}, Attempt {AttemptId}, Amount {Amount} {Currency}, Method {Method}",
            request.OrderId,
            request.PaymentAttemptId,
            request.Amount,
            request.Currency,
            request.PaymentMethod);

        // Check simulated test triggers
        var method = request.PaymentMethod.ToLowerInvariant();
        if (method.Contains("timeout"))
        {
            _logger.LogWarning("Simulated provider timeout for Attempt {AttemptId}. Returning status Unknown (retained as Success at provider for reconciliation).", request.PaymentAttemptId);
            _knownAttempts[request.PaymentAttemptId] = (PaymentProviderStatus.Success, $"mock_tx_{request.PaymentAttemptId:N}");
            return new PaymentChargeResult(PaymentProviderStatus.Unknown, null, "Provider call timed out; transaction status is unknown.");
        }

        if (method.Contains("network_error"))
        {
            _logger.LogWarning("Simulated network failure for Attempt {AttemptId}. Returning status Unknown (retained as Failed at provider for reconciliation).", request.PaymentAttemptId);
            _knownAttempts[request.PaymentAttemptId] = (PaymentProviderStatus.Failed, null);
            return new PaymentChargeResult(PaymentProviderStatus.Unknown, null, "Network connectivity failure; transaction status is unknown.");
        }

        if (method.Contains("decline") || method.Contains("fail"))
        {
            _logger.LogInformation("Simulated provider decline for Attempt {AttemptId}. Returning status Failed.", request.PaymentAttemptId);
            _knownAttempts[request.PaymentAttemptId] = (PaymentProviderStatus.Failed, null);
            return new PaymentChargeResult(PaymentProviderStatus.Failed, null, "Payment declined by provider: insufficient funds.");
        }

        // If BaseAddress is configured, invoke actual HTTP endpoint with resilience and timeout handling
        if (_httpClient.BaseAddress is not null)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/mock-provider/charges", request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PaymentChargeResult>(cancellationToken: cancellationToken);
                    if (result is not null)
                    {
                        _knownAttempts[request.PaymentAttemptId] = (result.Status, result.ProviderTransactionId);
                        return result;
                    }
                }

                if ((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                {
                    _logger.LogWarning("Provider returned server error status code {StatusCode}. Mapping to Unknown.", response.StatusCode);
                    return new PaymentChargeResult(PaymentProviderStatus.Unknown, null, $"Provider returned HTTP {(int)response.StatusCode}. Status is unknown.");
                }

                var errorMsg = await response.Content.ReadAsStringAsync(cancellationToken);
                return new PaymentChargeResult(PaymentProviderStatus.Failed, null, errorMsg);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Request timed out via HTTP resilience / CancellationTokenSource
                _logger.LogWarning(ex, "Provider call timed out for Attempt {AttemptId}. Mapping to Unknown.", request.PaymentAttemptId);
                _knownAttempts[request.PaymentAttemptId] = (PaymentProviderStatus.Unknown, null);
                return new PaymentChargeResult(PaymentProviderStatus.Unknown, null, "Provider HTTP call timed out. Status is unknown.");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP connection error for Attempt {AttemptId}. Mapping to Unknown.", request.PaymentAttemptId);
                _knownAttempts[request.PaymentAttemptId] = (PaymentProviderStatus.Unknown, null);
                return new PaymentChargeResult(PaymentProviderStatus.Unknown, null, $"HTTP connection error: {ex.Message}. Status is unknown.");
            }
        }

        // Default mock success
        var transactionId = $"mock_tx_{Guid.NewGuid():N}";
        _knownAttempts[request.PaymentAttemptId] = (PaymentProviderStatus.Success, transactionId);

        _logger.LogInformation(
            "External payment provider approved charge for Attempt {AttemptId} with TransactionId {TransactionId}",
            request.PaymentAttemptId,
            transactionId);

        return await Task.FromResult(new PaymentChargeResult(PaymentProviderStatus.Success, transactionId, null));
    }

    public async Task<PaymentStatusQueryResult> GetStatusAsync(PaymentStatusQueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Querying provider status for Attempt {AttemptId}, TransactionId {TransactionId}",
            request.PaymentAttemptId,
            request.ProviderTransactionId);

        if (_knownAttempts.TryGetValue(request.PaymentAttemptId, out var known))
        {
            return await Task.FromResult(new PaymentStatusQueryResult(known.Status, known.TransactionId, null));
        }

        // If not found, return Unknown so reconciliation can query again later
        return await Task.FromResult(new PaymentStatusQueryResult(PaymentProviderStatus.Unknown, null, "Attempt not yet registered with provider."));
    }

    public async Task<PaymentRefundResult> RefundAsync(PaymentRefundRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Processing refund for Attempt {AttemptId}, ProviderTransactionId {TransactionId}, Amount {Amount} {Currency}",
            request.PaymentAttemptId,
            request.ProviderTransactionId,
            request.Amount,
            request.Currency);

        if (request.Reason.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return await Task.FromResult(new PaymentRefundResult(PaymentProviderStatus.Unknown, null, "Refund request timed out."));
        }

        var refundId = $"mock_ref_{Guid.NewGuid():N}";
        return await Task.FromResult(new PaymentRefundResult(PaymentProviderStatus.Success, refundId, null));
    }
}

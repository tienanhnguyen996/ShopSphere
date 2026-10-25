using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShopSphere.Services.Payment.Application.Common.Interfaces;
using ShopSphere.Services.Payment.Infrastructure.Configuration;

namespace ShopSphere.Services.Payment.Infrastructure.Clients;

public class MoMoPaymentProviderClient : IPaymentProviderClient
{
    private readonly HttpClient _httpClient;
    private readonly MoMoSettings _settings;
    private readonly ILogger<MoMoPaymentProviderClient> _logger;

    public MoMoPaymentProviderClient(
        HttpClient httpClient,
        IOptions<PaymentSettings> settings,
        ILogger<MoMoPaymentProviderClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value.MoMo ?? throw new ArgumentNullException(nameof(settings));
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
            "Calling MoMo Gateway for Order {OrderId}, Attempt {AttemptId}, Amount {Amount} {Currency}",
            request.OrderId,
            request.PaymentAttemptId,
            request.Amount,
            request.Currency);

        var amountVnd = ConvertToVnd(request.Amount, request.Currency);
        var orderId = $"SS_{request.PaymentAttemptId.ToString("N")[..20]}";
        var requestId = request.PaymentAttemptId.ToString();
        var orderInfo = $"ShopSphere Order #{orderId}";
        var extraData = string.Empty;
        var requestType = "captureWallet";

        // Raw signature string format defined by MoMo standard API:
        // accessKey=$accessKey&amount=$amount&extraData=$extraData&ipnUrl=$ipnUrl&orderId=$orderId&orderInfo=$orderInfo&partnerCode=$partnerCode&redirectUrl=$redirectUrl&requestId=$requestId&requestType=$requestType
        var rawHash = $"accessKey={_settings.AccessKey}&amount={amountVnd}&extraData={extraData}&ipnUrl={_settings.NotifyUrl}&orderId={orderId}&orderInfo={orderInfo}&partnerCode={_settings.PartnerCode}&redirectUrl={_settings.ReturnUrl}&requestId={requestId}&requestType={requestType}";
        var signature = ComputeHmacSha256(rawHash, _settings.SecretKey);

        var payload = new
        {
            partnerCode = _settings.PartnerCode,
            partnerName = "ShopSphere",
            storeId = "ShopSphereStore",
            requestId,
            amount = amountVnd,
            orderId,
            orderInfo,
            redirectUrl = _settings.ReturnUrl,
            ipnUrl = _settings.NotifyUrl,
            lang = "en",
            extraData,
            requestType,
            signature
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("create", payload, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var jsonNode = JsonNode.Parse(content);
                var resultCode = jsonNode?["resultCode"]?.GetValue<int>() ?? -1;
                var message = jsonNode?["message"]?.ToString();
                var transId = jsonNode?["transId"]?.ToString() ?? jsonNode?["orderId"]?.ToString();

                if (resultCode == 0)
                {
                    _logger.LogInformation("MoMo payment created successfully for Attempt {AttemptId}: {TransId}", request.PaymentAttemptId, transId);
                    return new PaymentChargeResult(PaymentProviderStatus.Success, $"momo_{transId ?? requestId}", null);
                }

                _logger.LogWarning("MoMo payment creation rejected with code {ResultCode}: {Message}", resultCode, message);
                return new PaymentChargeResult(PaymentProviderStatus.Failed, null, $"MoMo error [{resultCode}]: {message}");
            }

            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout)
            {
                _logger.LogWarning("MoMo returned server error {StatusCode} for Attempt {AttemptId}. Mapping to Unknown.", response.StatusCode, request.PaymentAttemptId);
                return new PaymentChargeResult(PaymentProviderStatus.Unknown, null, $"MoMo API returned HTTP {(int)response.StatusCode}. Status is unknown.");
            }

            return new PaymentChargeResult(PaymentProviderStatus.Failed, null, $"MoMo HTTP {(int)response.StatusCode}: {content}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("MoMo API call timed out for Attempt {AttemptId}. Mapping to Unknown.", request.PaymentAttemptId);
            return new PaymentChargeResult(PaymentProviderStatus.Unknown, null, "MoMo API call timed out; transaction status is unknown.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "MoMo network error for Attempt {AttemptId}. Mapping to Unknown.", request.PaymentAttemptId);
            return new PaymentChargeResult(PaymentProviderStatus.Unknown, null, $"MoMo network failure: {ex.Message}");
        }
    }

    public async Task<PaymentStatusQueryResult> GetStatusAsync(PaymentStatusQueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestId = request.PaymentAttemptId.ToString();
        var rawOrderId = request.ProviderTransactionId?.Replace("momo_", "", StringComparison.OrdinalIgnoreCase) ?? requestId;

        var rawHash = $"accessKey={_settings.AccessKey}&orderId={rawOrderId}&partnerCode={_settings.PartnerCode}&requestId={requestId}";
        var signature = ComputeHmacSha256(rawHash, _settings.SecretKey);

        var payload = new
        {
            partnerCode = _settings.PartnerCode,
            requestId,
            orderId = rawOrderId,
            signature,
            lang = "en"
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("query", payload, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var jsonNode = JsonNode.Parse(content);
                var resultCode = jsonNode?["resultCode"]?.GetValue<int>() ?? -1;
                var message = jsonNode?["message"]?.ToString();
                var transId = jsonNode?["transId"]?.ToString() ?? rawOrderId;

                if (resultCode == 0)
                {
                    return new PaymentStatusQueryResult(PaymentProviderStatus.Success, $"momo_{transId}", null);
                }

                if (resultCode is 1000 or 9000)
                {
                    return new PaymentStatusQueryResult(PaymentProviderStatus.Unknown, $"momo_{transId}", $"MoMo payment is pending: {message}");
                }

                return new PaymentStatusQueryResult(PaymentProviderStatus.Failed, $"momo_{transId}", $"MoMo status [{resultCode}]: {message}");
            }

            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout)
            {
                return new PaymentStatusQueryResult(PaymentProviderStatus.Unknown, request.ProviderTransactionId, $"MoMo query HTTP {(int)response.StatusCode}");
            }

            return new PaymentStatusQueryResult(PaymentProviderStatus.Failed, request.ProviderTransactionId, content);
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException)
        {
            return new PaymentStatusQueryResult(PaymentProviderStatus.Unknown, request.ProviderTransactionId, $"MoMo query network error: {ex.Message}");
        }
    }

    public async Task<PaymentRefundResult> RefundAsync(PaymentRefundRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestId = Guid.NewGuid().ToString();
        var orderId = request.PaymentAttemptId.ToString();
        var amountVnd = (long)Math.Round(request.Amount, MidpointRounding.AwayFromZero);
        var cleanTransId = request.ProviderTransactionId.Replace("momo_", "", StringComparison.OrdinalIgnoreCase);
        var description = string.IsNullOrWhiteSpace(request.Reason) ? "ShopSphere Order Refund" : request.Reason;

        var rawHash = $"accessKey={_settings.AccessKey}&amount={amountVnd}&description={description}&orderId={orderId}&partnerCode={_settings.PartnerCode}&requestId={requestId}&transId={cleanTransId}";
        var signature = ComputeHmacSha256(rawHash, _settings.SecretKey);

        var payload = new
        {
            partnerCode = _settings.PartnerCode,
            orderId,
            requestId,
            amount = amountVnd,
            transId = cleanTransId,
            description,
            signature,
            lang = "en"
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("refund", payload, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var jsonNode = JsonNode.Parse(content);
                var resultCode = jsonNode?["resultCode"]?.GetValue<int>() ?? -1;
                var message = jsonNode?["message"]?.ToString();
                var refundTransId = jsonNode?["transId"]?.ToString() ?? requestId;

                if (resultCode == 0)
                {
                    return new PaymentRefundResult(PaymentProviderStatus.Success, $"momo_ref_{refundTransId}", null);
                }

                return new PaymentRefundResult(PaymentProviderStatus.Failed, null, $"MoMo refund failed [{resultCode}]: {message}");
            }

            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout)
            {
                return new PaymentRefundResult(PaymentProviderStatus.Unknown, null, $"MoMo refund HTTP {(int)response.StatusCode}");
            }

            return new PaymentRefundResult(PaymentProviderStatus.Failed, null, content);
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException)
        {
            return new PaymentRefundResult(PaymentProviderStatus.Unknown, null, $"MoMo refund network error: {ex.Message}");
        }
    }

    private static long ConvertToVnd(decimal amount, string currency)
    {
        if (currency.Equals("VND", StringComparison.OrdinalIgnoreCase))
        {
            return (long)Math.Round(amount, MidpointRounding.AwayFromZero);
        }

        // Standard test exchange rate: 1 USD ~ 25,400 VND
        return (long)Math.Round(amount * 25400m, MidpointRounding.AwayFromZero);
    }

    private static string ComputeHmacSha256(string message, string secretKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

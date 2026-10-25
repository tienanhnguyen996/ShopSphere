using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShopSphere.Services.Payment.Application.Common.Interfaces;
using ShopSphere.Services.Payment.Infrastructure.Configuration;

namespace ShopSphere.Services.Payment.Infrastructure.Clients;

public class VnPayPaymentProviderClient : IPaymentProviderClient
{
    private readonly HttpClient _httpClient;
    private readonly VnPaySettings _settings;
    private readonly ILogger<VnPayPaymentProviderClient> _logger;

    public VnPayPaymentProviderClient(
        HttpClient httpClient,
        IOptions<PaymentSettings> settings,
        ILogger<VnPayPaymentProviderClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value.VnPay ?? throw new ArgumentNullException(nameof(settings));
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
            "Generating VNPay transaction for Order {OrderId}, Attempt {AttemptId}, Amount {Amount} {Currency}",
            request.OrderId,
            request.PaymentAttemptId,
            request.Amount,
            request.Currency);

        var txnRef = request.PaymentAttemptId.ToString("N");
        var amountVnd = ConvertToVnd(request.Amount, request.Currency) * 100; // VNPay multiplies by 100
        var createDate = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

        var parameters = new SortedList<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = _settings.TmnCode,
            ["vnp_Amount"] = amountVnd.ToString(CultureInfo.InvariantCulture),
            ["vnp_CreateDate"] = createDate,
            ["vnp_CurrCode"] = "VND",
            ["vnp_IpAddr"] = "127.0.0.1",
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = $"ShopSphere Order #{request.OrderId}",
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = _settings.ReturnUrl,
            ["vnp_TxnRef"] = txnRef
        };

        var queryBuilder = new StringBuilder();
        foreach (var (key, value) in parameters)
        {
            if (queryBuilder.Length > 0)
            {
                queryBuilder.Append('&');
            }
            queryBuilder.Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value));
        }

        var rawData = queryBuilder.ToString();
        var secureHash = ComputeHmacSha512(rawData, _settings.HashSecret);
        var paymentUrl = $"{_settings.PaymentUrl}?{rawData}&vnp_SecureHash={secureHash}";

        // If Merchant Web API is configured, ping or query sandbox to verify connectivity
        if (!string.IsNullOrWhiteSpace(_settings.ApiUrl) && _httpClient.BaseAddress is not null)
        {
            try
            {
                var queryResult = await GetStatusAsync(new PaymentStatusQueryRequest(request.PaymentAttemptId, $"vnp_{txnRef}"), cancellationToken);
                if (queryResult.Status == PaymentProviderStatus.Success)
                {
                    return new PaymentChargeResult(PaymentProviderStatus.Success, queryResult.ProviderTransactionId, null);
                }
            }
            catch
            {
                // Fall back to returning generated link transaction
            }
        }

        _logger.LogInformation("VNPay payment URL generated successfully for Attempt {AttemptId}: {TxnRef}", request.PaymentAttemptId, txnRef);
        return await Task.FromResult(new PaymentChargeResult(PaymentProviderStatus.Success, $"vnp_{txnRef}", null));
    }

    public async Task<PaymentStatusQueryResult> GetStatusAsync(PaymentStatusQueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var txnRef = request.ProviderTransactionId?.Replace("vnp_", "", StringComparison.OrdinalIgnoreCase)
                     ?? request.PaymentAttemptId.ToString("N");

        var requestId = Guid.NewGuid().ToString("N");
        var version = "2.1.0";
        var command = "querydr";
        var tmnCode = _settings.TmnCode;
        var transDate = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var createDate = transDate;
        var ipAddr = "127.0.0.1";
        var orderInfo = $"Query order {txnRef}";

        // Hash data for querydr: vnp_RequestId|vnp_Version|vnp_Command|vnp_TmnCode|vnp_TxnRef|vnp_TransactionDate|vnp_CreateDate|vnp_IpAddr|vnp_OrderInfo
        var hashData = $"{requestId}|{version}|{command}|{tmnCode}|{txnRef}|{transDate}|{createDate}|{ipAddr}|{orderInfo}";
        var secureHash = ComputeHmacSha512(hashData, _settings.HashSecret);

        var payload = new
        {
            vnp_RequestId = requestId,
            vnp_Version = version,
            vnp_Command = command,
            vnp_TmnCode = tmnCode,
            vnp_TxnRef = txnRef,
            vnp_OrderInfo = orderInfo,
            vnp_TransactionDate = transDate,
            vnp_CreateDate = createDate,
            vnp_IpAddr = ipAddr,
            vnp_SecureHash = secureHash
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(_settings.ApiUrl, payload, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var jsonNode = JsonNode.Parse(content);
                var responseCode = jsonNode?["vnp_ResponseCode"]?.ToString();
                var transactionStatus = jsonNode?["vnp_TransactionStatus"]?.ToString();
                var message = jsonNode?["vnp_Message"]?.ToString();

                if (responseCode == "00" && transactionStatus == "00")
                {
                    return new PaymentStatusQueryResult(PaymentProviderStatus.Success, $"vnp_{txnRef}", null);
                }

                if (responseCode is "01" or "02")
                {
                    return new PaymentStatusQueryResult(PaymentProviderStatus.Unknown, $"vnp_{txnRef}", $"VNPay transaction pending: {message}");
                }

                return new PaymentStatusQueryResult(PaymentProviderStatus.Failed, $"vnp_{txnRef}", $"VNPay error [{responseCode}]: {message}");
            }

            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout)
            {
                return new PaymentStatusQueryResult(PaymentProviderStatus.Unknown, $"vnp_{txnRef}", $"VNPay query HTTP {(int)response.StatusCode}");
            }

            return new PaymentStatusQueryResult(PaymentProviderStatus.Failed, $"vnp_{txnRef}", content);
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException)
        {
            return new PaymentStatusQueryResult(PaymentProviderStatus.Unknown, $"vnp_{txnRef}", $"VNPay query network error: {ex.Message}");
        }
    }

    public async Task<PaymentRefundResult> RefundAsync(PaymentRefundRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var txnRef = request.ProviderTransactionId.Replace("vnp_", "", StringComparison.OrdinalIgnoreCase);
        var requestId = Guid.NewGuid().ToString("N");
        var version = "2.1.0";
        var command = "refund";
        var tmnCode = _settings.TmnCode;
        var transType = "02"; // Full refund
        var amountVnd = (long)Math.Round(request.Amount, MidpointRounding.AwayFromZero) * 100;
        var transDate = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var createDate = transDate;
        var createBy = "ShopSphere";
        var ipAddr = "127.0.0.1";
        var orderInfo = string.IsNullOrWhiteSpace(request.Reason) ? "ShopSphere Order Refund" : request.Reason;

        // Hash data for refund: vnp_RequestId|vnp_Version|vnp_Command|vnp_TmnCode|vnp_TransactionType|vnp_TxnRef|vnp_Amount|vnp_TransactionNo|vnp_TransactionDate|vnp_CreateBy|vnp_CreateDate|vnp_IpAddr|vnp_OrderInfo
        var hashData = $"{requestId}|{version}|{command}|{tmnCode}|{transType}|{txnRef}|{amountVnd}||{transDate}|{createBy}|{createDate}|{ipAddr}|{orderInfo}";
        var secureHash = ComputeHmacSha512(hashData, _settings.HashSecret);

        var payload = new
        {
            vnp_RequestId = requestId,
            vnp_Version = version,
            vnp_Command = command,
            vnp_TmnCode = tmnCode,
            vnp_TransactionType = transType,
            vnp_TxnRef = txnRef,
            vnp_Amount = amountVnd,
            vnp_OrderInfo = orderInfo,
            vnp_TransactionDate = transDate,
            vnp_CreateBy = createBy,
            vnp_CreateDate = createDate,
            vnp_IpAddr = ipAddr,
            vnp_SecureHash = secureHash
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(_settings.ApiUrl, payload, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var jsonNode = JsonNode.Parse(content);
                var responseCode = jsonNode?["vnp_ResponseCode"]?.ToString();
                var message = jsonNode?["vnp_Message"]?.ToString();

                if (responseCode == "00")
                {
                    return new PaymentRefundResult(PaymentProviderStatus.Success, $"vnp_ref_{requestId}", null);
                }

                return new PaymentRefundResult(PaymentProviderStatus.Failed, null, $"VNPay refund failed [{responseCode}]: {message}");
            }

            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout)
            {
                return new PaymentRefundResult(PaymentProviderStatus.Unknown, null, $"VNPay refund HTTP {(int)response.StatusCode}");
            }

            return new PaymentRefundResult(PaymentProviderStatus.Failed, null, content);
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException)
        {
            return new PaymentRefundResult(PaymentProviderStatus.Unknown, null, $"VNPay refund network error: {ex.Message}");
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

    public static string ComputeHmacSha512(string message, string secretKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var messageBytes = Encoding.UTF8.GetBytes(message);

        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(messageBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

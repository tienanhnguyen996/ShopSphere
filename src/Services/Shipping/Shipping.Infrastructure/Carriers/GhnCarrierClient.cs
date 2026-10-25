using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShopSphere.Services.Shipping.Application.Abstractions;
using ShopSphere.Services.Shipping.Domain;
using ShopSphere.Services.Shipping.Infrastructure.Configuration;

namespace ShopSphere.Services.Shipping.Infrastructure.Carriers;

public class GhnCarrierClient : ICarrierProviderClient
{
    private readonly HttpClient _httpClient;
    private readonly GhnSettings _settings;
    private readonly ILogger<GhnCarrierClient> _logger;

    public GhnCarrierClient(
        HttpClient httpClient,
        IOptions<CarrierSettings> settings,
        ILogger<GhnCarrierClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value.Ghn ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/') + "/");
        }
    }

    public async Task<CarrierLabelResult> RequestLabelAsync(
        Guid orderId,
        string carrierRequestId,
        ShippingAddress recipientAddress,
        IReadOnlyCollection<ShipmentItem> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipientAddress);

        _logger.LogInformation("Calling GHN API to create shipping order for Order {OrderId}", orderId);

        var payload = new
        {
            payment_type_id = 1,
            note = $"ShopSphere Order #{orderId:N}",
            required_note = "KHONGCHOXEMHANG",
            to_name = recipientAddress.RecipientName,
            to_phone = string.IsNullOrWhiteSpace(recipientAddress.Phone) ? "0901234567" : recipientAddress.Phone,
            to_address = recipientAddress.Street,
            to_ward_code = "20308",
            to_district_id = 1442,
            weight = 1000,
            length = 20,
            width = 15,
            height = 10,
            service_type_id = 2,
            items = items.Select(i => new
            {
                name = i.ProductName,
                quantity = i.Quantity,
                weight = 500
            }).ToArray()
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "shipping-order/create")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("Token", _settings.Token);
        request.Headers.Add("ShopId", _settings.ShopId.ToString());

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var jsonNode = JsonNode.Parse(content);
                var code = jsonNode?["code"]?.GetValue<int>() ?? 0;
                var message = jsonNode?["message"]?.ToString();
                var data = jsonNode?["data"];
                var orderCode = data?["order_code"]?.ToString();
                var expectedDelivery = data?["expected_delivery_time"]?.ToString();

                if (code == 200 && !string.IsNullOrWhiteSpace(orderCode))
                {
                    DateTime? deliveryTime = DateTime.TryParse(expectedDelivery, out var parsed) ? parsed.ToUniversalTime() : DateTime.UtcNow.AddDays(2);
                    var labelUrl = $"https://dev-online-gateway.ghn.vn/shiip/public-api/v2/a5/gen-token?order_codes={orderCode}";

                    _logger.LogInformation("GHN order created successfully with code: {OrderCode}", orderCode);
                    return new CarrierLabelResult(
                        CarrierLabelStatus.Created,
                        carrierRequestId,
                        "GHN",
                        orderCode,
                        labelUrl,
                        deliveryTime,
                        null);
                }

                _logger.LogWarning("GHN order rejected with code {Code}: {Message}", code, message);
                return new CarrierLabelResult(CarrierLabelStatus.Failed, carrierRequestId, "GHN", null, null, null, $"GHN error [{code}]: {message}");
            }

            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout)
            {
                _logger.LogWarning("GHN returned server error {StatusCode} for Order {OrderId}. Mapping to UnknownTimeout.", response.StatusCode, orderId);
                return new CarrierLabelResult(CarrierLabelStatus.UnknownTimeout, carrierRequestId, "GHN", null, null, null, "GHN service timeout.");
            }

            return new CarrierLabelResult(CarrierLabelStatus.Failed, carrierRequestId, "GHN", null, null, null, content);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("GHN API call timed out for Order {OrderId}. Mapping to UnknownTimeout.", orderId);
            return new CarrierLabelResult(CarrierLabelStatus.UnknownTimeout, carrierRequestId, "GHN", null, null, null, "GHN request timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "GHN network error for Order {OrderId}. Mapping to UnknownTimeout.", orderId);
            return new CarrierLabelResult(CarrierLabelStatus.UnknownTimeout, carrierRequestId, "GHN", null, null, null, $"GHN network failure: {ex.Message}");
        }
    }

    public async Task<CarrierLabelResult> QueryLabelStatusAsync(string carrierRequestId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carrierRequestId);

        var payload = new { order_code = carrierRequestId };
        using var request = new HttpRequestMessage(HttpMethod.Post, "shipping-order/detail")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("Token", _settings.Token);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var jsonNode = JsonNode.Parse(content);
                var code = jsonNode?["code"]?.GetValue<int>() ?? 0;
                var data = jsonNode?["data"];
                var status = data?["status"]?.ToString();
                var orderCode = data?["order_code"]?.ToString() ?? carrierRequestId;

                if (code == 200 && !string.IsNullOrWhiteSpace(status))
                {
                    return new CarrierLabelResult(
                        CarrierLabelStatus.Created,
                        carrierRequestId,
                        "GHN",
                        orderCode,
                        $"https://dev-online-gateway.ghn.vn/shiip/public-api/v2/a5/gen-token?order_codes={orderCode}",
                        DateTime.UtcNow.AddDays(2),
                        null);
                }

                return new CarrierLabelResult(CarrierLabelStatus.Failed, carrierRequestId, "GHN", null, null, null, $"GHN status query returned code {code}");
            }

            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout)
            {
                return new CarrierLabelResult(CarrierLabelStatus.UnknownTimeout, carrierRequestId, "GHN", null, null, null, "GHN query timed out.");
            }

            return new CarrierLabelResult(CarrierLabelStatus.Failed, carrierRequestId, "GHN", null, null, null, content);
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException)
        {
            return new CarrierLabelResult(CarrierLabelStatus.UnknownTimeout, carrierRequestId, "GHN", null, null, null, ex.Message);
        }
    }
}

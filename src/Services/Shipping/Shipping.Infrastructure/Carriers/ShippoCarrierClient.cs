using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShopSphere.Services.Shipping.Application.Abstractions;
using ShopSphere.Services.Shipping.Domain;
using ShopSphere.Services.Shipping.Infrastructure.Configuration;

namespace ShopSphere.Services.Shipping.Infrastructure.Carriers;

public class ShippoCarrierClient : ICarrierProviderClient
{
    private readonly HttpClient _httpClient;
    private readonly ShippoSettings _settings;
    private readonly ILogger<ShippoCarrierClient> _logger;

    public ShippoCarrierClient(
        HttpClient httpClient,
        IOptions<CarrierSettings> settings,
        ILogger<ShippoCarrierClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings?.Value.Shippo ?? throw new ArgumentNullException(nameof(settings));
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

        _logger.LogInformation("Calling Shippo API to create shipment and purchase label for Order {OrderId}", orderId);

        var shipmentPayload = new
        {
            address_from = new
            {
                name = "ShopSphere Logistics Hub",
                street1 = "215 Clayton St",
                city = "San Francisco",
                state = "CA",
                zip = "94117",
                country = "US",
                phone = "+1 555 341 9393",
                email = "shipping@shopsphere.io"
            },
            address_to = new
            {
                name = recipientAddress.RecipientName,
                street1 = recipientAddress.Street,
                city = recipientAddress.City,
                state = recipientAddress.State,
                zip = recipientAddress.PostalCode,
                country = recipientAddress.Country,
                phone = recipientAddress.Phone
            },
            parcels = new[]
            {
                new
                {
                    length = "20",
                    width = "15",
                    height = "10",
                    distance_unit = "cm",
                    weight = "1.5",
                    mass_unit = "kg"
                }
            },
            @async = false
        };

        try
        {
            using var createShipmentRequest = new HttpRequestMessage(HttpMethod.Post, "shipments/")
            {
                Content = JsonContent.Create(shipmentPayload)
            };
            createShipmentRequest.Headers.Authorization = new AuthenticationHeaderValue("ShippoToken", _settings.ApiKey);

            using var shipmentResponse = await _httpClient.SendAsync(createShipmentRequest, cancellationToken);
            var shipmentContent = await shipmentResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!shipmentResponse.IsSuccessStatusCode)
            {
                if ((int)shipmentResponse.StatusCode >= 500 || shipmentResponse.StatusCode == HttpStatusCode.RequestTimeout)
                {
                    _logger.LogWarning("Shippo shipments endpoint timed out / returned 5xx for Order {OrderId}. Mapping to UnknownTimeout.", orderId);
                    return new CarrierLabelResult(CarrierLabelStatus.UnknownTimeout, carrierRequestId, "Shippo", null, null, null, "Shippo service timeout.");
                }

                _logger.LogWarning("Shippo shipment creation failed with HTTP {StatusCode}: {Content}", shipmentResponse.StatusCode, shipmentContent);
                return new CarrierLabelResult(CarrierLabelStatus.Failed, carrierRequestId, "Shippo", null, null, null, $"Shippo shipment error: {shipmentContent}");
            }

            var shipmentJson = JsonNode.Parse(shipmentContent);
            var rates = shipmentJson?["rates"]?.AsArray();
            var lowestRate = rates?.FirstOrDefault();
            var rateId = lowestRate?["object_id"]?.ToString();
            var provider = lowestRate?["provider"]?.ToString() ?? "Shippo";

            if (string.IsNullOrWhiteSpace(rateId))
            {
                return new CarrierLabelResult(CarrierLabelStatus.Failed, carrierRequestId, provider, null, null, null, "No shipping rates returned by carrier.");
            }

            // Purchase transaction label using the selected rate
            var transactionPayload = new
            {
                rate = rateId,
                @async = false
            };

            using var buyTransactionRequest = new HttpRequestMessage(HttpMethod.Post, "transactions/")
            {
                Content = JsonContent.Create(transactionPayload)
            };
            buyTransactionRequest.Headers.Authorization = new AuthenticationHeaderValue("ShippoToken", _settings.ApiKey);

            using var transactionResponse = await _httpClient.SendAsync(buyTransactionRequest, cancellationToken);
            var transactionContent = await transactionResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!transactionResponse.IsSuccessStatusCode)
            {
                if ((int)transactionResponse.StatusCode >= 500 || transactionResponse.StatusCode == HttpStatusCode.RequestTimeout)
                {
                    _logger.LogWarning("Shippo transaction buy timed out for Order {OrderId}. Mapping to UnknownTimeout.", orderId);
                    return new CarrierLabelResult(CarrierLabelStatus.UnknownTimeout, carrierRequestId, provider, null, null, null, "Shippo transaction timeout.");
                }

                return new CarrierLabelResult(CarrierLabelStatus.Failed, carrierRequestId, provider, null, null, null, transactionContent);
            }

            var txJson = JsonNode.Parse(transactionContent);
            var txStatus = txJson?["status"]?.ToString();
            var trackingNumber = txJson?["tracking_number"]?.ToString();
            var labelUrl = txJson?["label_url"]?.ToString();
            var eta = DateTime.UtcNow.AddDays(4);

            if (txStatus == "SUCCESS" && !string.IsNullOrWhiteSpace(trackingNumber))
            {
                _logger.LogInformation("Shippo label purchased successfully: {TrackingNumber}", trackingNumber);
                return new CarrierLabelResult(
                    CarrierLabelStatus.Created,
                    carrierRequestId,
                    provider,
                    trackingNumber,
                    labelUrl,
                    eta,
                    null);
            }

            return new CarrierLabelResult(
                CarrierLabelStatus.Failed,
                carrierRequestId,
                provider,
                trackingNumber,
                labelUrl,
                null,
                $"Shippo transaction status: {txStatus}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Shippo API call timed out for Order {OrderId}. Mapping to UnknownTimeout.", orderId);
            return new CarrierLabelResult(CarrierLabelStatus.UnknownTimeout, carrierRequestId, "Shippo", null, null, null, "Shippo request timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Shippo network error for Order {OrderId}. Mapping to UnknownTimeout.", orderId);
            return new CarrierLabelResult(CarrierLabelStatus.UnknownTimeout, carrierRequestId, "Shippo", null, null, null, $"Shippo network failure: {ex.Message}");
        }
    }

    public async Task<CarrierLabelResult> QueryLabelStatusAsync(string carrierRequestId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carrierRequestId);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"transactions/{carrierRequestId}");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("ShippoToken", _settings.ApiKey);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = JsonNode.Parse(content);
                var status = json?["status"]?.ToString();
                var trackingNumber = json?["tracking_number"]?.ToString();
                var labelUrl = json?["label_url"]?.ToString();

                if (status == "SUCCESS")
                {
                    return new CarrierLabelResult(CarrierLabelStatus.Created, carrierRequestId, "Shippo", trackingNumber, labelUrl, DateTime.UtcNow.AddDays(3), null);
                }

                if (status is "QUEUED" or "WAITING")
                {
                    return new CarrierLabelResult(CarrierLabelStatus.UnknownTimeout, carrierRequestId, "Shippo", null, null, null, "Label generation in progress.");
                }

                return new CarrierLabelResult(CarrierLabelStatus.Failed, carrierRequestId, "Shippo", null, null, null, $"Shippo status: {status}");
            }

            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.RequestTimeout)
            {
                return new CarrierLabelResult(CarrierLabelStatus.UnknownTimeout, carrierRequestId, "Shippo", null, null, null, "Shippo status query timed out.");
            }

            return new CarrierLabelResult(CarrierLabelStatus.Failed, carrierRequestId, "Shippo", null, null, null, content);
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException)
        {
            return new CarrierLabelResult(CarrierLabelStatus.UnknownTimeout, carrierRequestId, "Shippo", null, null, null, ex.Message);
        }
    }
}

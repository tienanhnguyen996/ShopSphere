using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShopSphere.Services.Shipping.Application.Abstractions;
using ShopSphere.Services.Shipping.Domain;
using ShopSphere.Services.Shipping.Infrastructure.Configuration;

namespace ShopSphere.Services.Shipping.Infrastructure.Carriers;

public class RoutingCarrierProviderClient : ICarrierProviderClient
{
    private readonly ShippoCarrierClient _shippoClient;
    private readonly GhnCarrierClient _ghnClient;
    private readonly MockCarrierProviderClient _mockClient;
    private readonly CarrierSettings _settings;
    private readonly ILogger<RoutingCarrierProviderClient> _logger;

    public RoutingCarrierProviderClient(
        ShippoCarrierClient shippoClient,
        GhnCarrierClient ghnClient,
        MockCarrierProviderClient mockClient,
        IOptions<CarrierSettings> settings,
        ILogger<RoutingCarrierProviderClient> logger)
    {
        _shippoClient = shippoClient ?? throw new ArgumentNullException(nameof(shippoClient));
        _ghnClient = ghnClient ?? throw new ArgumentNullException(nameof(ghnClient));
        _mockClient = mockClient ?? throw new ArgumentNullException(nameof(mockClient));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<CarrierLabelResult> RequestLabelAsync(
        Guid orderId,
        string carrierRequestId,
        ShippingAddress recipientAddress,
        IReadOnlyCollection<ShipmentItem> items,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipientAddress);

        var country = recipientAddress.Country.Trim().ToUpperInvariant();

        if (country is "VN" or "VIETNAM" or "VNM")
        {
            if (_settings.Ghn.IsConfigured)
            {
                _logger.LogInformation("Routing shipment {OrderId} to GHN carrier for destination {Country}", orderId, country);
                return _ghnClient.RequestLabelAsync(orderId, carrierRequestId, recipientAddress, items, cancellationToken);
            }

            if (_settings.FallbackToMockOnMissingCredentials)
            {
                _logger.LogWarning("GHN credentials not configured. Falling back to MockCarrierProviderClient for Order {OrderId}", orderId);
                return _mockClient.RequestLabelAsync(orderId, carrierRequestId, recipientAddress, items, cancellationToken);
            }

            return Task.FromResult(new CarrierLabelResult(
                CarrierLabelStatus.Failed,
                carrierRequestId,
                "GHN",
                null,
                null,
                null,
                "GHN carrier is not configured and fallback to mock is disabled."));
        }

        // International destination -> Shippo
        if (_settings.Shippo.IsConfigured)
        {
            _logger.LogInformation("Routing shipment {OrderId} to Shippo carrier for international destination {Country}", orderId, country);
            return _shippoClient.RequestLabelAsync(orderId, carrierRequestId, recipientAddress, items, cancellationToken);
        }

        if (_settings.FallbackToMockOnMissingCredentials)
        {
            _logger.LogWarning("Shippo credentials not configured. Falling back to MockCarrierProviderClient for Order {OrderId}", orderId);
            return _mockClient.RequestLabelAsync(orderId, carrierRequestId, recipientAddress, items, cancellationToken);
        }

        return Task.FromResult(new CarrierLabelResult(
            CarrierLabelStatus.Failed,
            carrierRequestId,
            "Shippo",
            null,
            null,
            null,
            "Shippo carrier is not configured and fallback to mock is disabled."));
    }

    public Task<CarrierLabelResult> QueryLabelStatusAsync(string carrierRequestId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carrierRequestId);

        if (carrierRequestId.StartsWith("GHN", StringComparison.OrdinalIgnoreCase))
        {
            return _settings.Ghn.IsConfigured
                ? _ghnClient.QueryLabelStatusAsync(carrierRequestId, cancellationToken)
                : _mockClient.QueryLabelStatusAsync(carrierRequestId, cancellationToken);
        }

        if (carrierRequestId.StartsWith("shippo_", StringComparison.OrdinalIgnoreCase)
            || carrierRequestId.StartsWith("rate_", StringComparison.OrdinalIgnoreCase)
            || carrierRequestId.StartsWith("tx_", StringComparison.OrdinalIgnoreCase))
        {
            return _settings.Shippo.IsConfigured
                ? _shippoClient.QueryLabelStatusAsync(carrierRequestId, cancellationToken)
                : _mockClient.QueryLabelStatusAsync(carrierRequestId, cancellationToken);
        }

        return _mockClient.QueryLabelStatusAsync(carrierRequestId, cancellationToken);
    }
}

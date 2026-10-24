using ShopSphere.Services.Shipping.Application.Abstractions;
using ShopSphere.Services.Shipping.Domain;

namespace ShopSphere.Services.Shipping.Infrastructure.Carriers;

public sealed class MockCarrierProviderClient : ICarrierProviderClient
{
    public Task<CarrierLabelResult> RequestLabelAsync(
        Guid orderId,
        string carrierRequestId,
        ShippingAddress recipientAddress,
        IReadOnlyCollection<ShipmentItem> items,
        CancellationToken cancellationToken = default)
    {
        // Deterministic test hooks via RecipientName / PostalCode markers for timeout and failure simulation
        if (recipientAddress.PostalCode.Equals("00000-TIMEOUT", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new CarrierLabelResult(
                CarrierLabelStatus.UnknownTimeout,
                carrierRequestId,
                null,
                null,
                null,
                null,
                "Simulated carrier HTTP timeout waiting for shipping label response."));
        }

        if (recipientAddress.PostalCode.Equals("00000-FAIL", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new CarrierLabelResult(
                CarrierLabelStatus.Failed,
                carrierRequestId,
                null,
                null,
                null,
                null,
                "Carrier rejected destination postal code."));
        }

        var trackingNumber = $"SS-TRK-{orderId.ToString("N")[..12].ToUpperInvariant()}";
        var labelUrl = $"https://shipping.shopsphere.local/labels/{trackingNumber}.pdf";

        return Task.FromResult(new CarrierLabelResult(
            CarrierLabelStatus.Created,
            carrierRequestId,
            "ShopSphereExpress",
            trackingNumber,
            labelUrl,
            DateTime.UtcNow.AddDays(3),
            null));
    }

    public Task<CarrierLabelResult> QueryLabelStatusAsync(
        string carrierRequestId,
        CancellationToken cancellationToken = default)
    {
        var suffix = carrierRequestId.Length >= 12
            ? carrierRequestId[^12..].ToUpperInvariant()
            : carrierRequestId.ToUpperInvariant();
        var trackingNumber = $"SS-TRK-{suffix}";

        return Task.FromResult(new CarrierLabelResult(
            CarrierLabelStatus.Created,
            carrierRequestId,
            "ShopSphereExpress",
            trackingNumber,
            $"https://shipping.shopsphere.local/labels/{trackingNumber}.pdf",
            DateTime.UtcNow.AddDays(3),
            null));
    }
}

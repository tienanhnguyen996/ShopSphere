using ShopSphere.Services.Shipping.Domain;

namespace ShopSphere.Services.Shipping.Application.Abstractions;

public enum CarrierLabelStatus
{
    Created = 1,
    UnknownTimeout = 2,
    Failed = 3
}

public sealed record CarrierLabelResult(
    CarrierLabelStatus Status,
    string CarrierRequestId,
    string? Carrier,
    string? TrackingNumber,
    string? LabelUrl,
    DateTime? EstimatedDeliveryAtUtc,
    string? ErrorReason);

public interface ICarrierProviderClient
{
    Task<CarrierLabelResult> RequestLabelAsync(
        Guid orderId,
        string carrierRequestId,
        ShippingAddress recipientAddress,
        IReadOnlyCollection<ShipmentItem> items,
        CancellationToken cancellationToken = default);

    Task<CarrierLabelResult> QueryLabelStatusAsync(
        string carrierRequestId,
        CancellationToken cancellationToken = default);
}

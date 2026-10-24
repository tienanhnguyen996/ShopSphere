using ShopSphere.BuildingBlocks.Common.Domain;
using ShopSphere.Services.Shipping.Domain.Events;
using ShopSphere.Services.Shipping.Domain.Exceptions;

namespace ShopSphere.Services.Shipping.Domain;

/// <summary>
/// Aggregate root representing an order fulfillment shipment, carrier tracking label,
/// physical handover status, and delivery milestone timeline.
/// </summary>
public class Shipment : AggregateRoot<Guid>, IVersionedEntity, IAuditableEntity
{
    private readonly List<ShipmentItem> _items = new();
    private readonly List<TrackingMilestone> _milestones = new();

    public Guid OrderId { get; private set; }
    public string CustomerId { get; private set; } = string.Empty;
    public string Carrier { get; private set; } = string.Empty;
    public string? TrackingNumber { get; private set; }
    public string? ExternalShipmentRequestId { get; private set; }
    public ShipmentStatus Status { get; private set; }
    public ShippingAddress Address { get; private set; } = default!;
    public IReadOnlyCollection<ShipmentItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<TrackingMilestone> Milestones => _milestones.AsReadOnly();
    public DateTime? HandedOverAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }
    public string? FailureReason { get; private set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    private Shipment() { }

    private Shipment(
        Guid id,
        Guid orderId,
        string customerId,
        ShippingAddress address,
        IReadOnlyList<ShipmentItem> items,
        string carrier) : base(id)
    {
        if (id == Guid.Empty)
        {
            throw new ShippingDomainException("Shipment ID cannot be empty.");
        }

        if (orderId == Guid.Empty)
        {
            throw new ShippingDomainException("Order ID cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ShippingDomainException("Customer ID is required.");
        }

        if (address is null)
        {
            throw new ShippingDomainException("Shipping address is required.");
        }

        if (items is null || items.Count == 0)
        {
            throw new ShippingDomainException("Shipment must contain at least one item.");
        }

        OrderId = orderId;
        CustomerId = customerId.Trim();
        Address = address;
        Carrier = string.IsNullOrWhiteSpace(carrier) ? "ShopSphereExpress" : carrier.Trim();
        Status = ShipmentStatus.Pending;
        CreatedAtUtc = DateTime.UtcNow;

        _items.AddRange(items);
        _milestones.Add(TrackingMilestone.Create(
            Id,
            ShipmentStatus.Pending,
            "Fulfillment request received; preparing shipment label.",
            "ShopSphere Warehouse",
            CreatedAtUtc));
    }

    public static Shipment Create(
        Guid id,
        Guid orderId,
        string customerId,
        ShippingAddress address,
        IEnumerable<ShipmentItem> items,
        string carrier = "ShopSphereExpress")
    {
        var itemList = items?.ToList() ?? [];
        return new Shipment(id, orderId, customerId, address, itemList, carrier);
    }

    public void AssignTrackingLabel(string trackingNumber, string? externalRequestId = null)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            throw new ShippingDomainException("Tracking number is required.");
        }

        if (Status == ShipmentStatus.LabelCreated &&
            string.Equals(TrackingNumber, trackingNumber.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Status is not (ShipmentStatus.Pending or ShipmentStatus.ShipmentUnknown))
        {
            throw new InvalidShipmentStateTransitionException(
                Status,
                ShipmentStatus.LabelCreated,
                $"Cannot assign tracking label to shipment {Id} in state {Status}.");
        }

        TrackingNumber = trackingNumber.Trim();
        if (!string.IsNullOrWhiteSpace(externalRequestId))
        {
            ExternalShipmentRequestId = externalRequestId.Trim();
        }

        FailureReason = null;
        Status = ShipmentStatus.LabelCreated;
        UpdatedAtUtc = DateTime.UtcNow;

        _milestones.Add(TrackingMilestone.Create(
            Id,
            ShipmentStatus.LabelCreated,
            $"Shipping label created with tracking number {TrackingNumber}.",
            "ShopSphere Warehouse",
            UpdatedAtUtc));

        AddDomainEvent(new ShipmentCreatedDomainEvent(Id, OrderId, CustomerId, TrackingNumber));
    }

    public void MarkUnknown(string externalRequestId, string reason)
    {
        if (string.IsNullOrWhiteSpace(externalRequestId))
        {
            throw new ShippingDomainException("External shipment request ID is required for ShipmentUnknown state.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ShippingDomainException("Reason is required for ShipmentUnknown state.");
        }

        if (Status is not (ShipmentStatus.Pending or ShipmentStatus.ShipmentUnknown))
        {
            throw new InvalidShipmentStateTransitionException(
                Status,
                ShipmentStatus.ShipmentUnknown,
                $"Cannot transition shipment {Id} from {Status} to {ShipmentStatus.ShipmentUnknown}.");
        }

        ExternalShipmentRequestId = externalRequestId.Trim();
        FailureReason = reason.Trim();
        Status = ShipmentStatus.ShipmentUnknown;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ShippingDomainException("Failure reason is required.");
        }

        if (Status == ShipmentStatus.Failed)
        {
            return;
        }

        if (Status is ShipmentStatus.HandedOver or ShipmentStatus.InTransit or ShipmentStatus.Delivered)
        {
            throw new InvalidShipmentStateTransitionException(
                Status,
                ShipmentStatus.Failed,
                $"Cannot mark shipment {Id} as failed after physical carrier handover ({Status}).");
        }

        FailureReason = reason.Trim();
        Status = ShipmentStatus.Failed;
        UpdatedAtUtc = DateTime.UtcNow;

        _milestones.Add(TrackingMilestone.Create(
            Id,
            ShipmentStatus.Failed,
            $"Shipment failed: {FailureReason}",
            null,
            UpdatedAtUtc));
    }

    public void RecordCarrierHandover(string? location = null)
    {
        if (Status == ShipmentStatus.HandedOver)
        {
            return;
        }

        if (Status != ShipmentStatus.LabelCreated)
        {
            throw new InvalidShipmentStateTransitionException(
                Status,
                ShipmentStatus.HandedOver,
                $"Cannot hand over shipment {Id} in state {Status}; label must be created first.");
        }

        var now = DateTime.UtcNow;
        Status = ShipmentStatus.HandedOver;
        HandedOverAtUtc = now;
        UpdatedAtUtc = now;

        _milestones.Add(TrackingMilestone.Create(
            Id,
            ShipmentStatus.HandedOver,
            "Package physically handed over to carrier.",
            location ?? "Carrier Sorting Facility",
            now));

        AddDomainEvent(new ShipmentHandedOverDomainEvent(Id, OrderId, TrackingNumber ?? string.Empty, now));
    }

    public void RecordInTransit(string description, string? location = null)
    {
        if (Status is not (ShipmentStatus.HandedOver or ShipmentStatus.InTransit))
        {
            throw new InvalidShipmentStateTransitionException(
                Status,
                ShipmentStatus.InTransit,
                $"Cannot record in-transit milestone for shipment {Id} in state {Status}.");
        }

        var now = DateTime.UtcNow;
        Status = ShipmentStatus.InTransit;
        UpdatedAtUtc = now;

        _milestones.Add(TrackingMilestone.Create(
            Id,
            ShipmentStatus.InTransit,
            string.IsNullOrWhiteSpace(description) ? "Package in transit to destination." : description.Trim(),
            location,
            now));
    }

    public void RecordDelivered(string? location = null)
    {
        if (Status == ShipmentStatus.Delivered)
        {
            return;
        }

        if (Status is not (ShipmentStatus.LabelCreated or ShipmentStatus.HandedOver or ShipmentStatus.InTransit))
        {
            throw new InvalidShipmentStateTransitionException(
                Status,
                ShipmentStatus.Delivered,
                $"Cannot mark shipment {Id} as delivered from state {Status}.");
        }

        var now = DateTime.UtcNow;
        HandedOverAtUtc ??= now;
        DeliveredAtUtc = now;
        Status = ShipmentStatus.Delivered;
        UpdatedAtUtc = now;

        _milestones.Add(TrackingMilestone.Create(
            Id,
            ShipmentStatus.Delivered,
            "Package delivered to recipient.",
            location ?? $"{Address.City}, {Address.State}",
            now));

        AddDomainEvent(new ShipmentDeliveredDomainEvent(Id, OrderId, TrackingNumber ?? string.Empty, now));
    }

    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ShippingDomainException("Cancellation reason is required.");
        }

        if (Status == ShipmentStatus.Cancelled)
        {
            return;
        }

        // Critical invariant from 02-services.md:
        // "Once goods are physically handed over, a returns process is needed, not just rolling back a flag in the DB."
        if (Status is ShipmentStatus.HandedOver or ShipmentStatus.InTransit or ShipmentStatus.Delivered)
        {
            throw new InvalidShipmentStateTransitionException(
                Status,
                ShipmentStatus.Cancelled,
                $"Cannot cancel shipment {Id} in state {Status} because goods have already been physically handed over to the carrier.");
        }

        var now = DateTime.UtcNow;
        Status = ShipmentStatus.Cancelled;
        FailureReason = reason.Trim();
        UpdatedAtUtc = now;

        _milestones.Add(TrackingMilestone.Create(
            Id,
            ShipmentStatus.Cancelled,
            $"Shipment cancelled: {FailureReason}",
            null,
            now));
    }
}

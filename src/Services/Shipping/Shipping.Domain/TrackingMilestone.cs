using ShopSphere.BuildingBlocks.Common.Domain;

namespace ShopSphere.Services.Shipping.Domain;

public class TrackingMilestone : Entity<Guid>
{
    public Guid ShipmentId { get; private set; }
    public ShipmentStatus Status { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? Location { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    private TrackingMilestone() { }

    private TrackingMilestone(
        Guid id,
        Guid shipmentId,
        ShipmentStatus status,
        string description,
        string? location,
        DateTime occurredAtUtc) : base(id)
    {
        ShipmentId = shipmentId;
        Status = status;
        Description = description.Trim();
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        OccurredAtUtc = occurredAtUtc;
    }

    public static TrackingMilestone Create(
        Guid shipmentId,
        ShipmentStatus status,
        string description,
        string? location = null,
        DateTime? occurredAtUtc = null)
    {
        return new TrackingMilestone(
            Guid.NewGuid(),
            shipmentId,
            status,
            description,
            location,
            occurredAtUtc ?? DateTime.UtcNow);
    }
}

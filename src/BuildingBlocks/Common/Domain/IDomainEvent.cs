namespace ShopSphere.BuildingBlocks.Common.Domain;

/// <summary>
/// Marker interface for all domain events.
/// </summary>
public interface IDomainEvent
{
    Guid EventId => Guid.NewGuid();
    DateTime OccurredAtUtc => DateTime.UtcNow;
}

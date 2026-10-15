using ShopSphere.BuildingBlocks.Common.Domain;

namespace ShopSphere.Services.Catalog.Domain.Events;

public sealed record ProductPriceChangedDomainEvent(
    Guid ProductId,
    Sku Sku,
    Money OldPrice,
    Money NewPrice,
    int NewPriceVersion) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAtUtc { get; } = DateTime.UtcNow;
}

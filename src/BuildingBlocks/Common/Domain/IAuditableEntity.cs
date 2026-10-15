namespace ShopSphere.BuildingBlocks.Common.Domain;

/// <summary>
/// Interface for entities tracking creation and modification timestamps in UTC.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAtUtc { get; set; }
    DateTime? UpdatedAtUtc { get; set; }
}

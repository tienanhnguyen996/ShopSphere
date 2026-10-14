namespace ShopSphere.BuildingBlocks.Common.Domain;

/// <summary>
/// Concurrency token interface for optimistic concurrency with EF Core / SQL Server rowversion.
/// </summary>
public interface IVersionedEntity
{
    byte[] RowVersion { get; set; }
}

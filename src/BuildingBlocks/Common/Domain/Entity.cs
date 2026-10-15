namespace ShopSphere.BuildingBlocks.Common.Domain;

/// <summary>
/// Base class for all domain entities with a strongly typed identifier.
/// </summary>
public abstract class Entity<TId> where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    protected Entity() { }

    protected Entity(TId id)
    {
        if (EqualityComparer<TId>.Default.Equals(id, default))
        {
            throw new ArgumentException("Entity identifier cannot be default/empty.", nameof(id));
        }

        if (id is Guid g && g == Guid.Empty)
        {
            throw new ArgumentException("Entity identifier cannot be default/empty.", nameof(id));
        }

        if (id is string s && string.IsNullOrWhiteSpace(s))
        {
            throw new ArgumentException("Entity identifier cannot be empty or whitespace.", nameof(id));
        }

        Id = id;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (GetType() != other.GetType())
        {
            return false;
        }

        return EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override int GetHashCode() => EqualityComparer<TId>.Default.GetHashCode(Id);

    public static bool operator ==(Entity<TId>? a, Entity<TId>? b) => a?.Equals(b) ?? b is null;

    public static bool operator !=(Entity<TId>? a, Entity<TId>? b) => !(a == b);
}

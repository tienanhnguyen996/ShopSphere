namespace ShopSphere.BuildingBlocks.Idempotency;

public class IdempotencyRecord
{
    public string Key { get; init; } = string.Empty;
    public string RequestFingerprint { get; init; } = string.Empty;
    public int ResponseStatusCode { get; init; }
    public string? ResponseBody { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; init; }
}

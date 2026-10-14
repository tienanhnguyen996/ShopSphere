namespace ShopSphere.BuildingBlocks.Idempotency;

public class IdempotentRequest
{
    public string Key { get; set; } = string.Empty;
    public string OperationCode { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public string? ResponseBody { get; set; }
    public int? StatusCode { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

using System.Security.Cryptography;
using System.Text;

namespace ShopSphere.BuildingBlocks.Idempotency;

public static class IdempotencyFingerprint
{
    public static string Compute(string httpMethod, string route, string payload)
    {
        var normalizedMethod = (httpMethod ?? string.Empty).Trim().ToUpperInvariant();
        var normalizedRoute = (route ?? string.Empty).Trim().TrimEnd('/').ToLowerInvariant();
        var normalizedPayload = payload ?? string.Empty;

        var raw = $"{normalizedMethod}:{normalizedRoute}:{normalizedPayload}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

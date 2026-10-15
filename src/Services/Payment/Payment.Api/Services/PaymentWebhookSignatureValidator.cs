using System.Security.Cryptography;
using System.Text;

namespace ShopSphere.Services.Payment.Api.Services;

public interface IPaymentWebhookSignatureValidator
{
    bool ValidateSignature(string payload, string signatureHeader, string secret);
    string ComputeSignature(string payload, string secret);
}

public class PaymentWebhookSignatureValidator : IPaymentWebhookSignatureValidator
{
    public bool ValidateSignature(string payload, string signatureHeader, string secret)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        var cleanSignature = signatureHeader.Trim();
        if (cleanSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            cleanSignature = cleanSignature.Substring(7);
        }

        var expectedSignature = ComputeSignature(payload, secret);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(cleanSignature.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(expectedSignature.ToLowerInvariant()));
    }

    public string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

namespace ShopSphere.Services.Payment.Infrastructure.Configuration;

public class StripeSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.stripe.com/v1/";
    public string WebhookSecret { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

public class MoMoSettings
{
    public string PartnerCode { get; set; } = "MOMOBKUN20180529";
    public string AccessKey { get; set; } = "klm05TvNBzhg7h7j";
    public string SecretKey { get; set; } = "at67qH6mk8w5Y1nAyMoYKMWACiEi2bsa";
    public string BaseUrl { get; set; } = "https://test-payment.momo.vn/v2/gateway/api/";
    public string ReturnUrl { get; set; } = "http://localhost:3000/orders/confirmation";
    public string NotifyUrl { get; set; } = "http://localhost:5000/api/v1/payment/webhooks/momo";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(PartnerCode)
                             && !string.IsNullOrWhiteSpace(AccessKey)
                             && !string.IsNullOrWhiteSpace(SecretKey);
}

public class VnPaySettings
{
    public string TmnCode { get; set; } = "DEMOVNPAY";
    public string HashSecret { get; set; } = "RAZUIDIBAARAYOPUPFDICPHGKWUZGPUT";
    public string BaseUrl { get; set; } = "https://sandbox.vnpayment.vn/";
    public string PaymentUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
    public string ApiUrl { get; set; } = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";
    public string ReturnUrl { get; set; } = "http://localhost:3000/orders/confirmation";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(TmnCode) && !string.IsNullOrWhiteSpace(HashSecret);
}

public class PaymentSettings
{
    public const string SectionName = "PaymentSettings";

    public StripeSettings Stripe { get; set; } = new();
    public MoMoSettings MoMo { get; set; } = new();
    public VnPaySettings VnPay { get; set; } = new();
    public bool FallbackToMockOnMissingCredentials { get; set; } = true;
}

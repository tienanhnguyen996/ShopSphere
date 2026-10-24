namespace ShopSphere.Services.Shipping.Infrastructure.Configuration;

public class ShippoSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.goshippo.com/";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}

public class GhnSettings
{
    public string Token { get; set; } = string.Empty;
    public int ShopId { get; set; } = 0;
    public string BaseUrl { get; set; } = "https://dev-online-gateway.ghn.vn/shiip/public-api/v2/";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Token) && ShopId > 0;
}

public class CarrierSettings
{
    public const string SectionName = "CarrierSettings";

    public ShippoSettings Shippo { get; set; } = new();
    public GhnSettings Ghn { get; set; } = new();
    public bool FallbackToMockOnMissingCredentials { get; set; } = true;
}

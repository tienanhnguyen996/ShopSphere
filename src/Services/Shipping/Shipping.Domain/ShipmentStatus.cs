namespace ShopSphere.Services.Shipping.Domain;

public enum ShipmentStatus
{
    Pending = 0,
    LabelCreated = 1,
    ShipmentUnknown = 2,
    HandedOver = 3,
    InTransit = 4,
    Delivered = 5,
    Cancelled = 6,
    Failed = 7
}

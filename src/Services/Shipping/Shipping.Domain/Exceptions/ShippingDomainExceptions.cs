namespace ShopSphere.Services.Shipping.Domain.Exceptions;

public class ShippingDomainException : Exception
{
    public ShippingDomainException(string message) : base(message) { }
}

public class InvalidShipmentStateTransitionException : ShippingDomainException
{
    public ShipmentStatus CurrentStatus { get; }
    public ShipmentStatus TargetStatus { get; }

    public InvalidShipmentStateTransitionException(
        ShipmentStatus currentStatus,
        ShipmentStatus targetStatus,
        string message) : base(message)
    {
        CurrentStatus = currentStatus;
        TargetStatus = targetStatus;
    }
}

public class ShipmentNotFoundException : ShippingDomainException
{
    public Guid ShipmentId { get; }

    public ShipmentNotFoundException(Guid shipmentId)
        : base($"Shipment '{shipmentId}' was not found.")
    {
        ShipmentId = shipmentId;
    }
}

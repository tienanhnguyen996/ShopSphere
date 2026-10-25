namespace ShopSphere.Services.Order.Domain.Exceptions;

public class OrderDomainException : Exception
{
    public OrderDomainException(string message) : base(message) { }
}

public class InvalidOrderStateTransitionException : OrderDomainException
{
    public OrderStatus CurrentStatus { get; }
    public OrderStatus TargetStatus { get; }

    public InvalidOrderStateTransitionException(
        OrderStatus currentStatus,
        OrderStatus targetStatus,
        string message) : base(message)
    {
        CurrentStatus = currentStatus;
        TargetStatus = targetStatus;
    }
}

namespace ShopSphere.Services.Payment.Domain.Exceptions;

public class PaymentDomainException : Exception
{
    public PaymentDomainException(string message) : base(message)
    {
    }

    public PaymentDomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class InvalidPaymentStateTransitionException : PaymentDomainException
{
    public PaymentStatus CurrentStatus { get; }
    public PaymentStatus TargetStatus { get; }

    public InvalidPaymentStateTransitionException(PaymentStatus currentStatus, PaymentStatus targetStatus, string message)
        : base(message)
    {
        CurrentStatus = currentStatus;
        TargetStatus = targetStatus;
    }
}

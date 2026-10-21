namespace ShopSphere.Services.Identity.Domain.Exceptions;

public class IdentityDomainException : Exception
{
    public IdentityDomainException(string message) : base(message)
    {
    }

    public IdentityDomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

namespace ShopSphere.Services.Cart.Domain.Exceptions;

public class DomainValidationException : Exception
{
    public string Code { get; }

    public DomainValidationException(string code, string message) : base(message)
    {
        Code = code;
    }

    public DomainValidationException(string message) : this("Validation.Failed", message)
    {
    }
}

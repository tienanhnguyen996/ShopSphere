using ShopSphere.BuildingBlocks.Common.Domain;
using ShopSphere.Services.Shipping.Domain.Exceptions;

namespace ShopSphere.Services.Shipping.Domain;

public sealed class ShippingAddress : ValueObject
{
    public string RecipientName { get; private set; } = string.Empty;
    public string Street { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
    public string Country { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;

    private ShippingAddress() { }

    private ShippingAddress(
        string recipientName,
        string street,
        string city,
        string state,
        string postalCode,
        string country,
        string phone)
    {
        if (string.IsNullOrWhiteSpace(recipientName))
        {
            throw new ShippingDomainException("Recipient name is required.");
        }

        if (string.IsNullOrWhiteSpace(street))
        {
            throw new ShippingDomainException("Street address is required.");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new ShippingDomainException("City is required.");
        }

        if (string.IsNullOrWhiteSpace(postalCode))
        {
            throw new ShippingDomainException("Postal code is required.");
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            throw new ShippingDomainException("Country is required.");
        }

        RecipientName = recipientName.Trim();
        Street = street.Trim();
        City = city.Trim();
        State = (state ?? string.Empty).Trim();
        PostalCode = postalCode.Trim();
        Country = country.Trim().ToUpperInvariant();
        Phone = (phone ?? string.Empty).Trim();
    }

    public static ShippingAddress Create(
        string recipientName,
        string street,
        string city,
        string state,
        string postalCode,
        string country,
        string phone = "")
    {
        return new ShippingAddress(recipientName, street, city, state, postalCode, country, phone);
    }

    protected override IEnumerable<object?> GetAtomicValues()
    {
        yield return RecipientName;
        yield return Street;
        yield return City;
        yield return State;
        yield return PostalCode;
        yield return Country;
        yield return Phone;
    }
}

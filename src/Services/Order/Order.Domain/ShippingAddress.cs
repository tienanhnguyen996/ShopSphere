using ShopSphere.BuildingBlocks.Common.Domain;
using ShopSphere.Services.Order.Domain.Exceptions;

namespace ShopSphere.Services.Order.Domain;

/// <summary>
/// Value object representing the destination shipping address captured at checkout.
/// </summary>
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
            throw new OrderDomainException("Recipient name is required.");
        }

        if (string.IsNullOrWhiteSpace(street))
        {
            throw new OrderDomainException("Street address is required.");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new OrderDomainException("City is required.");
        }

        if (string.IsNullOrWhiteSpace(postalCode))
        {
            throw new OrderDomainException("Postal code is required.");
        }

        if (string.IsNullOrWhiteSpace(country))
        {
            throw new OrderDomainException("Country is required.");
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

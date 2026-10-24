using ShopSphere.BuildingBlocks.Common.Results;

namespace ShopSphere.Services.Order.Application.Common.Interfaces;

public record CartQuoteItemDto(
    string Sku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string Currency,
    int PriceVersion,
    bool IsActive);

public record CartCheckoutQuoteDto(
    string CustomerId,
    IReadOnlyCollection<CartQuoteItemDto> Items,
    decimal Subtotal,
    string Currency,
    IReadOnlyDictionary<string, int> PriceVersions,
    bool IsValid,
    IReadOnlyCollection<string> ValidationErrors);

public interface ICartClient
{
    Task<Result<CartCheckoutQuoteDto>> GetCheckoutQuoteAsync(string customerId, CancellationToken ct = default);
    Task<Result> ClearCartAsync(string customerId, CancellationToken ct = default);
}

using CartAggregate = ShopSphere.Services.Cart.Domain.Cart;

namespace ShopSphere.Services.Cart.Application.Common.Interfaces;

public interface ICartRepository
{
    Task<CartAggregate?> GetCartAsync(string customerId, CancellationToken ct = default);
    Task<CartAggregate> SaveCartAsync(CartAggregate cart, CancellationToken ct = default);
    Task<bool> ClearCartAsync(string customerId, CancellationToken ct = default);
}

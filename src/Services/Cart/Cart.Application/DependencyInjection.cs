using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.Services.Cart.Application.Carts.Commands.AddItem;
using ShopSphere.Services.Cart.Application.Carts.Commands.ClearCart;
using ShopSphere.Services.Cart.Application.Carts.Commands.RemoveItem;
using ShopSphere.Services.Cart.Application.Carts.Commands.UpdateQuantity;
using ShopSphere.Services.Cart.Application.Carts.Queries.GetCart;
using ShopSphere.Services.Cart.Application.Carts.Queries.GetCheckoutQuote;

namespace ShopSphere.Services.Cart.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCartApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IGetCartQueryHandler, GetCartQueryHandler>();
        services.AddScoped<IAddItemToCartCommandHandler, AddItemToCartCommandHandler>();
        services.AddScoped<IUpdateCartItemQuantityCommandHandler, UpdateCartItemQuantityCommandHandler>();
        services.AddScoped<IRemoveCartItemCommandHandler, RemoveCartItemCommandHandler>();
        services.AddScoped<IClearCartCommandHandler, ClearCartCommandHandler>();
        services.AddScoped<IGetCheckoutQuoteQueryHandler, GetCheckoutQuoteQueryHandler>();

        return services;
    }
}

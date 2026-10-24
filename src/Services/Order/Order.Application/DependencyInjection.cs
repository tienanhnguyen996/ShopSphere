using Microsoft.Extensions.DependencyInjection;
using ShopSphere.Services.Order.Application.Orders.Commands.CancelOrder;
using ShopSphere.Services.Order.Application.Orders.Commands.SubmitCheckout;
using ShopSphere.Services.Order.Application.Orders.Queries.GetOrderById;
using ShopSphere.Services.Order.Application.Orders.Queries.GetOrders;

namespace ShopSphere.Services.Order.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddOrderApplication(this IServiceCollection services)
    {
        services.AddScoped<ISubmitCheckoutCommandHandler, SubmitCheckoutCommandHandler>();
        services.AddScoped<ICancelOrderCommandHandler, CancelOrderCommandHandler>();
        services.AddScoped<IGetOrderByIdQueryHandler, GetOrderByIdQueryHandler>();
        services.AddScoped<IGetOrdersQueryHandler, GetOrdersQueryHandler>();

        return services;
    }
}

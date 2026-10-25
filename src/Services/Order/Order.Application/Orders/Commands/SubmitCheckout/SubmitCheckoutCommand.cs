using MassTransit;
using Microsoft.Extensions.Logging;
using ShopSphere.BuildingBlocks.Common.Results;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Order.Application.Common.Interfaces;
using ShopSphere.Services.Order.Application.Contracts;
using ShopSphere.Services.Order.Application.Orders.Dtos;
using ShopSphere.Services.Order.Domain;
using ShopSphere.Services.Order.Domain.Exceptions;
using ShopSphere.Services.Shipping.Application.Contracts;

namespace ShopSphere.Services.Order.Application.Orders.Commands.SubmitCheckout;

public record SubmitCheckoutCommand(
    string CustomerId,
    ShippingAddressRequest ShippingAddress,
    string PaymentMethod);

public interface ISubmitCheckoutCommandHandler
{
    Task<Result<OrderDto>> HandleAsync(SubmitCheckoutCommand command, CancellationToken ct = default);
}

public class SubmitCheckoutCommandHandler : ISubmitCheckoutCommandHandler
{
    private readonly IOrderDbContext _dbContext;
    private readonly ICartClient _cartClient;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<SubmitCheckoutCommandHandler> _logger;

    public SubmitCheckoutCommandHandler(
        IOrderDbContext dbContext,
        ICartClient cartClient,
        IPublishEndpoint publishEndpoint,
        ILogger<SubmitCheckoutCommandHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cartClient = cartClient ?? throw new ArgumentNullException(nameof(cartClient));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<OrderDto>> HandleAsync(SubmitCheckoutCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.CustomerId))
        {
            return Result.Failure<OrderDto>(Error.Validation("Order.CustomerIdRequired", "Customer ID is required."));
        }

        if (command.ShippingAddress is null)
        {
            return Result.Failure<OrderDto>(Error.Validation("Order.ShippingAddressRequired", "Shipping address is required."));
        }

        if (string.IsNullOrWhiteSpace(command.PaymentMethod))
        {
            return Result.Failure<OrderDto>(Error.Validation("Order.PaymentMethodRequired", "Payment method is required."));
        }

        // 1. Fetch & verify live Cart Quote from Cart Service
        var quoteResult = await _cartClient.GetCheckoutQuoteAsync(command.CustomerId, ct);
        if (quoteResult.IsFailure)
        {
            return Result.Failure<OrderDto>(quoteResult.Error);
        }

        var quote = quoteResult.Value;
        if (!quote.IsValid || quote.Items.Count == 0)
        {
            var errors = quote.ValidationErrors.Count > 0
                ? string.Join("; ", quote.ValidationErrors)
                : "Cart is empty or contains invalid items.";
            return Result.Failure<OrderDto>(Error.Validation("Order.InvalidCartQuote", errors));
        }

        // 2. Construct Order aggregate and price snapshot items
        var orderId = Guid.NewGuid();
        ShopSphere.Services.Order.Domain.Order order;
        try
        {
            var shippingAddress = ShippingAddress.Create(
                command.ShippingAddress.RecipientName,
                command.ShippingAddress.Street,
                command.ShippingAddress.City,
                command.ShippingAddress.State,
                command.ShippingAddress.PostalCode,
                command.ShippingAddress.Country,
                command.ShippingAddress.Phone);

            var snapshotItems = quote.Items
                .Select(i => OrderItem.Create(
                    orderId,
                    i.Sku,
                    i.ProductName,
                    i.Quantity,
                    i.UnitPrice,
                    i.Currency,
                    i.PriceVersion))
                .ToList();

            order = ShopSphere.Services.Order.Domain.Order.Create(
                orderId,
                command.CustomerId,
                shippingAddress,
                command.PaymentMethod,
                snapshotItems);
        }
        catch (OrderDomainException ex)
        {
            return Result.Failure<OrderDto>(Error.Validation("Order.DomainValidation", ex.Message));
        }

        // 3. Persist Order + publish OrderSubmitted in the same transaction (Outbox)
        _dbContext.Orders.Add(order);

        var submittedEvent = new OrderSubmitted(
            order.Id,
            order.CustomerId,
            order.TotalAmount,
            order.Currency,
            order.PaymentMethod,
            new ShippingAddressDto(
                order.ShippingAddress.RecipientName,
                order.ShippingAddress.Street,
                order.ShippingAddress.City,
                order.ShippingAddress.State,
                order.ShippingAddress.PostalCode,
                order.ShippingAddress.Country,
                order.ShippingAddress.Phone),
            order.Items
                .Select(i => new OrderItemSnapshotDto(
                    i.Sku,
                    i.ProductName,
                    i.Quantity,
                    i.UnitPrice,
                    i.LineTotal,
                    i.Currency,
                    i.PriceVersion))
                .ToList());

        var envelope = MessageEnvelope<OrderSubmitted>.Create(
            submittedEvent,
            order.Id.ToString(),
            correlationId: order.Id);

        await _publishEndpoint.Publish(envelope, ct);
        await _publishEndpoint.Publish(submittedEvent, ct);

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created Order {OrderId} for Customer {CustomerId} with Total {TotalAmount} {Currency}",
            order.Id, order.CustomerId, order.TotalAmount, order.Currency);

        // 4. Best-effort clear cart after order snapshot is committed
        _ = await _cartClient.ClearCartAsync(command.CustomerId, ct);

        return Result.Success(order.ToDto());
    }
}

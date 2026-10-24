using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShopSphere.Services.Inventory.Application.Contracts;
using ShopSphere.Services.Order.Application.Common.Interfaces;
using ShopSphere.Services.Order.Application.Contracts;
using ShopSphere.Services.Order.Application.Saga;
using ShopSphere.Services.Order.Domain;
using ShopSphere.Services.Order.Tests.Unit.Helpers;
using ShopSphere.Services.Payment.Application.Contracts;
using ShopSphere.Services.Shipping.Application.Contracts;
using Xunit;

namespace ShopSphere.Services.Order.Tests.Unit.Saga;

public class OrderStateSyncConsumerTests
{
    private static ShopSphere.Services.Order.Domain.Order CreateSampleOrder(Guid orderId) =>
        ShopSphere.Services.Order.Domain.Order.Create(
            orderId,
            "cust-100",
            ShippingAddress.Create("Jane Doe", "123 Market St", "San Francisco", "CA", "94105", "US", "555-0100"),
            "credit_card",
            [OrderItem.Create(orderId, "SKU-1", "Item 1", 1, 100.00m, "USD", 1)]);

    [Fact]
    public async Task ConsumeLifecycleEvents_ShouldSynchronizeOrderStatusFromPendingToConfirmed()
    {
        var orderId = Guid.NewGuid();
        var order = CreateSampleOrder(orderId);
        var orders = new List<ShopSphere.Services.Order.Domain.Order> { order };
        var mockSet = MockDbSetExtensions.CreateMockDbSet(orders);
        var dbContextMock = new Mock<IOrderDbContext>();
        dbContextMock.Setup(d => d.Orders).Returns(mockSet.Object);

        var consumer = new OrderStateSyncConsumer(dbContextMock.Object, NullLogger<OrderStateSyncConsumer>.Instance);

        // 1. OrderSubmitted -> Reserving
        await consumer.Consume(MockConsumeContext(new OrderSubmitted(
            orderId, "cust-100", 100m, "USD", "credit_card",
            new ShippingAddressDto("Jane Doe", "123 Market St", "San Francisco", "CA", "94105", "US", "555-0100"),
            [])));
        order.Status.Should().Be(OrderStatus.Reserving);

        // 2. InventoryReserved -> Paying
        await consumer.Consume(MockConsumeContext(new InventoryReserved(orderId, [], DateTime.UtcNow.AddMinutes(15))));
        order.Status.Should().Be(OrderStatus.Paying);

        // 3. PaymentPendingResolution -> PaymentUnknown
        var attemptId = Guid.NewGuid();
        await consumer.Consume(MockConsumeContext(new PaymentPendingResolution(orderId, attemptId, "Provider timeout")));
        order.Status.Should().Be(OrderStatus.PaymentUnknown);

        // 4. PaymentSucceeded -> PaymentAttemptId stored
        await consumer.Consume(MockConsumeContext(new PaymentSucceeded(orderId, attemptId, "tx_1", 100m, "USD")));
        order.PaymentAttemptId.Should().Be(attemptId);

        // 5. OrderConfirmed -> Confirmed with ShipmentId and TrackingNumber
        var shipmentId = Guid.NewGuid();
        await consumer.Consume(MockConsumeContext(new OrderConfirmed(orderId, "cust-100", 100m, "USD", shipmentId, "TRK-999")));
        order.Status.Should().Be(OrderStatus.Confirmed);
        order.ShipmentId.Should().Be(shipmentId);
        order.TrackingNumber.Should().Be("TRK-999");
    }

    [Fact]
    public async Task ConsumeLatePaymentSucceeded_WhenOrderAlreadyCancelled_ShouldTransitionOrderToRefunding()
    {
        var orderId = Guid.NewGuid();
        var order = CreateSampleOrder(orderId);
        order.MarkCancelled("Reservation expired");

        var orders = new List<ShopSphere.Services.Order.Domain.Order> { order };
        var mockSet = MockDbSetExtensions.CreateMockDbSet(orders);
        var dbContextMock = new Mock<IOrderDbContext>();
        dbContextMock.Setup(d => d.Orders).Returns(mockSet.Object);

        var consumer = new OrderStateSyncConsumer(dbContextMock.Object, NullLogger<OrderStateSyncConsumer>.Instance);
        var lateAttemptId = Guid.NewGuid();

        await consumer.Consume(MockConsumeContext(new PaymentSucceeded(orderId, lateAttemptId, "tx_late", 100m, "USD")));

        order.Status.Should().Be(OrderStatus.Refunding);
        order.PaymentAttemptId.Should().Be(lateAttemptId);
    }

    private static ConsumeContext<T> MockConsumeContext<T>(T message) where T : class
    {
        var mock = new Mock<ConsumeContext<T>>();
        mock.Setup(c => c.Message).Returns(message);
        mock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }
}

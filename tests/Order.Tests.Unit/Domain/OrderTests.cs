using FluentAssertions;
using ShopSphere.Services.Order.Domain;
using ShopSphere.Services.Order.Domain.Events;
using ShopSphere.Services.Order.Domain.Exceptions;
using Xunit;

namespace ShopSphere.Services.Order.Tests.Unit.Domain;

public class OrderTests
{
    private static ShippingAddress CreateSampleAddress() =>
        ShippingAddress.Create(
            "Jane Doe",
            "123 Market St",
            "San Francisco",
            "CA",
            "94105",
            "US",
            "+1-415-555-0199");

    private static List<OrderItem> CreateSampleItems(Guid orderId) =>
    [
        OrderItem.Create(orderId, "SKU-LAPTOP-01", "Pro Laptop 15", 1, 1299.99m, "USD", 3),
        OrderItem.Create(orderId, "SKU-MOUSE-02", "Wireless Mouse", 2, 49.50m, "USD", 1)
    ];

    [Fact]
    public void Create_WithValidParameters_ShouldInitializePendingOrderWithCalculatedTotalAndSnapshotItems()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var address = CreateSampleAddress();
        var items = CreateSampleItems(orderId);

        // Act
        var order = ShopSphere.Services.Order.Domain.Order.Create(
            orderId,
            "customer-123",
            address,
            "credit_card",
            items);

        // Assert
        order.Id.Should().Be(orderId);
        order.CustomerId.Should().Be("customer-123");
        order.Status.Should().Be(OrderStatus.Pending);
        order.Currency.Should().Be("USD");
        order.PaymentMethod.Should().Be("credit_card");
        order.TotalAmount.Should().Be(1398.99m);
        order.Items.Should().HaveCount(2);
        order.Items.First(i => i.Sku == "SKU-LAPTOP-01").PriceVersion.Should().Be(3);
        order.Items.First(i => i.Sku == "SKU-MOUSE-02").LineTotal.Should().Be(99.00m);
        order.ShippingAddress.Should().Be(address);
        order.DomainEvents.Should().ContainSingle(e => e is OrderCreatedDomainEvent);
    }

    [Fact]
    public void Create_WithEmptyItems_ShouldThrowOrderDomainException()
    {
        // Arrange
        var address = CreateSampleAddress();

        // Act
        var act = () => ShopSphere.Services.Order.Domain.Order.Create(
            Guid.NewGuid(),
            "customer-123",
            address,
            "credit_card",
            []);

        // Assert
        act.Should().Throw<OrderDomainException>()
            .WithMessage("*at least one item*");
    }

    [Fact]
    public void Create_WithMismatchedItemCurrencies_ShouldThrowOrderDomainException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var address = CreateSampleAddress();
        var items = new[]
        {
            OrderItem.Create(orderId, "SKU-1", "Item 1", 1, 10m, "USD", 1),
            OrderItem.Create(orderId, "SKU-2", "Item 2", 1, 10m, "EUR", 1)
        };

        // Act
        var act = () => ShopSphere.Services.Order.Domain.Order.Create(
            orderId,
            "customer-123",
            address,
            "credit_card",
            items);

        // Assert
        act.Should().Throw<OrderDomainException>()
            .WithMessage("*same currency*");
    }

    [Fact]
    public void HappyPathTransitions_PendingToReservingToPayingToConfirmed_ShouldSucceed()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = ShopSphere.Services.Order.Domain.Order.Create(
            orderId,
            "customer-123",
            CreateSampleAddress(),
            "credit_card",
            CreateSampleItems(orderId));

        var paymentAttemptId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();

        // Act & Assert
        order.MarkReserving();
        order.Status.Should().Be(OrderStatus.Reserving);

        order.MarkPaying();
        order.Status.Should().Be(OrderStatus.Paying);

        order.MarkPaymentConfirmed(paymentAttemptId);
        order.PaymentAttemptId.Should().Be(paymentAttemptId);

        order.MarkConfirmed(shipmentId, "TRK-999888");
        order.Status.Should().Be(OrderStatus.Confirmed);
        order.ShipmentId.Should().Be(shipmentId);
        order.TrackingNumber.Should().Be("TRK-999888");
    }

    [Fact]
    public void MarkPaymentUnknown_WhenPaying_ShouldTransitionToPaymentUnknownAndRecordAttemptId()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = ShopSphere.Services.Order.Domain.Order.Create(
            orderId,
            "customer-123",
            CreateSampleAddress(),
            "credit_card",
            CreateSampleItems(orderId));
        order.MarkReserving();
        order.MarkPaying();

        var attemptId = Guid.NewGuid();

        // Act
        order.MarkPaymentUnknown(attemptId, "Provider timed out after 5000ms");

        // Assert
        order.Status.Should().Be(OrderStatus.PaymentUnknown);
        order.PaymentAttemptId.Should().Be(attemptId);
        order.FailureReason.Should().Be("Provider timed out after 5000ms");
    }

    [Fact]
    public void MarkConfirmed_FromPaymentUnknown_ShouldSucceedAfterReconciliation()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = ShopSphere.Services.Order.Domain.Order.Create(
            orderId,
            "customer-123",
            CreateSampleAddress(),
            "credit_card",
            CreateSampleItems(orderId));
        order.MarkReserving();
        order.MarkPaying();
        var attemptId = Guid.NewGuid();
        order.MarkPaymentUnknown(attemptId, "Provider timed out");

        // Act
        order.MarkPaymentConfirmed(attemptId);
        var shipmentId = Guid.NewGuid();
        order.MarkConfirmed(shipmentId, "TRK-RECON-01");

        // Assert
        order.Status.Should().Be(OrderStatus.Confirmed);
        order.FailureReason.Should().BeNull();
        order.ShipmentId.Should().Be(shipmentId);
        order.TrackingNumber.Should().Be("TRK-RECON-01");
    }

    [Fact]
    public void MarkConfirmed_WhenAlreadyCancelled_ShouldThrowInvalidOrderStateTransitionException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = ShopSphere.Services.Order.Domain.Order.Create(
            orderId,
            "customer-123",
            CreateSampleAddress(),
            "credit_card",
            CreateSampleItems(orderId));
        order.MarkCancelled("Out of stock");

        // Act - Late event trying to confirm a cancelled order must be rejected!
        var act = () => order.MarkConfirmed(Guid.NewGuid(), "TRK-LATE");

        // Assert
        act.Should().Throw<InvalidOrderStateTransitionException>();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void MarkRefunding_WhenCancelledAndLatePaymentArrives_ShouldTransitionToRefundingForCompensation()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = ShopSphere.Services.Order.Domain.Order.Create(
            orderId,
            "customer-123",
            CreateSampleAddress(),
            "credit_card",
            CreateSampleItems(orderId));
        order.MarkReserving();
        order.MarkPaying();
        order.MarkCancelled("Inventory reservation expired");

        var latePaymentAttemptId = Guid.NewGuid();

        // Act
        order.MarkRefunding("Late payment received on cancelled order", latePaymentAttemptId);

        // Assert
        order.Status.Should().Be(OrderStatus.Refunding);
        order.PaymentAttemptId.Should().Be(latePaymentAttemptId);
        order.CancellationReason.Should().Be("Late payment received on cancelled order");
    }

    [Fact]
    public void MarkCancelled_WhenConfirmed_ShouldThrowInvalidOrderStateTransitionException()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = ShopSphere.Services.Order.Domain.Order.Create(
            orderId,
            "customer-123",
            CreateSampleAddress(),
            "credit_card",
            CreateSampleItems(orderId));
        order.MarkReserving();
        order.MarkPaying();
        order.MarkConfirmed(Guid.NewGuid(), "TRK-100");

        // Act
        var act = () => order.MarkCancelled("Customer requested cancel after shipment");

        // Assert
        act.Should().Throw<InvalidOrderStateTransitionException>();
    }
}

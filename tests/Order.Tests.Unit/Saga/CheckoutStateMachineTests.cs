using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using ShopSphere.Services.Inventory.Application.Contracts;
using ShopSphere.Services.Order.Application.Contracts;
using ShopSphere.Services.Order.Application.Saga;
using ShopSphere.Services.Payment.Application.Contracts;
using ShopSphere.Services.Shipping.Application.Contracts;
using Xunit;

namespace ShopSphere.Services.Order.Tests.Unit.Saga;

public class CheckoutStateMachineTests
{
    private static OrderSubmitted CreateOrderSubmitted(Guid orderId) =>
        new(
            orderId,
            "cust-100",
            1250.00m,
            "USD",
            "credit_card",
            new ShippingAddressDto("Jane Doe", "123 Main St", "Seattle", "WA", "98101", "US", "555-0100"),
            [new OrderItemSnapshotDto("SKU-PRO-1", "Pro Laptop", 1, 1250.00m, 1250.00m, "USD", 1)]);

    [Fact]
    public async Task HappyPath_ShouldTransitionFromReservingToPayingToCommittingToShippingToConfirmed()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<CheckoutStateMachine, CheckoutSagaState>()
                    .InMemoryRepository();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var orderId = Guid.NewGuid();
        var paymentAttemptId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();
        var sagaHarness = harness.GetSagaStateMachineHarness<CheckoutStateMachine, CheckoutSagaState>();

        // 1. OrderSubmitted -> Reserving + ReserveInventory
        await harness.Bus.Publish(CreateOrderSubmitted(orderId));
        (await sagaHarness.Created.Any(x => x.CorrelationId == orderId)).Should().BeTrue();
        (await sagaHarness.Exists(orderId, x => x.Reserving)).Should().Be(orderId);
        (await harness.Published.Any<ReserveInventory>(x => x.Context.Message.OrderId == orderId)).Should().BeTrue();

        // 2. InventoryReserved -> Paying + CollectPayment
        await harness.Bus.Publish(new InventoryReserved(
            orderId,
            [new ReservedItemDto("SKU-PRO-1", 1)],
            DateTime.UtcNow.AddMinutes(15)));

        (await sagaHarness.Exists(orderId, x => x.Paying)).Should().Be(orderId);
        (await harness.Published.Any<CollectPayment>(x =>
            x.Context.Message.OrderId == orderId &&
            x.Context.Message.Amount == 1250.00m)).Should().BeTrue();

        // 3. PaymentSucceeded -> Committing + CommitInventory
        await harness.Bus.Publish(new PaymentSucceeded(
            orderId,
            paymentAttemptId,
            "tx_live_001",
            1250.00m,
            "USD"));

        (await sagaHarness.Exists(orderId, x => x.Committing)).Should().Be(orderId);
        (await harness.Published.Any<CommitInventory>(x => x.Context.Message.OrderId == orderId)).Should().BeTrue();

        // 4. InventoryCommitted -> Shipping + CreateShipment
        await harness.Bus.Publish(new InventoryCommitted(
            orderId,
            [new ReservedItemDto("SKU-PRO-1", 1)]));

        (await sagaHarness.Exists(orderId, x => x.Shipping)).Should().Be(orderId);
        (await harness.Published.Any<CreateShipment>(x => x.Context.Message.OrderId == orderId)).Should().BeTrue();

        // 5. ShipmentCreated -> Confirmed + OrderConfirmed
        await harness.Bus.Publish(new ShipmentCreated(
            orderId,
            shipmentId,
            "TRK-SHIP-777"));

        (await sagaHarness.Exists(orderId, x => x.Confirmed)).Should().Be(orderId);
        (await harness.Published.Any<OrderConfirmed>(x =>
            x.Context.Message.OrderId == orderId &&
            x.Context.Message.ShipmentId == shipmentId &&
            x.Context.Message.TrackingNumber == "TRK-SHIP-777")).Should().BeTrue();
    }

    [Fact]
    public async Task InventoryRejected_ShouldTransitionToCancelledAndPublishOrderCancelled()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<CheckoutStateMachine, CheckoutSagaState>()
                    .InMemoryRepository();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var orderId = Guid.NewGuid();
        var sagaHarness = harness.GetSagaStateMachineHarness<CheckoutStateMachine, CheckoutSagaState>();

        await harness.Bus.Publish(CreateOrderSubmitted(orderId));
        (await sagaHarness.Exists(orderId, x => x.Reserving)).Should().Be(orderId);

        await harness.Bus.Publish(new InventoryRejected(orderId, "Insufficient stock", ["SKU-PRO-1"]));

        (await sagaHarness.Exists(orderId, x => x.Cancelled)).Should().Be(orderId);
        (await harness.Published.Any<OrderCancelled>(x => x.Context.Message.OrderId == orderId)).Should().BeTrue();
    }

    [Fact]
    public async Task PaymentRejected_ShouldPublishReleaseInventoryAndTransitionToCancelled()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<CheckoutStateMachine, CheckoutSagaState>()
                    .InMemoryRepository();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var orderId = Guid.NewGuid();
        var sagaHarness = harness.GetSagaStateMachineHarness<CheckoutStateMachine, CheckoutSagaState>();

        await harness.Bus.Publish(CreateOrderSubmitted(orderId));
        (await sagaHarness.Exists(orderId, x => x.Reserving)).Should().Be(orderId);

        await harness.Bus.Publish(new InventoryReserved(orderId, [new ReservedItemDto("SKU-PRO-1", 1)], DateTime.UtcNow.AddMinutes(15)));
        (await sagaHarness.Exists(orderId, x => x.Paying)).Should().Be(orderId);

        await harness.Bus.Publish(new PaymentRejected(orderId, Guid.NewGuid(), "Card declined"));

        (await sagaHarness.Exists(orderId, x => x.Cancelled)).Should().Be(orderId);
        (await harness.Published.Any<ReleaseInventory>(x => x.Context.Message.OrderId == orderId)).Should().BeTrue();
        (await harness.Published.Any<OrderCancelled>(x => x.Context.Message.OrderId == orderId)).Should().BeTrue();
    }

    [Fact]
    public async Task PaymentPendingResolution_ShouldTransitionToPaymentUnknownAndProceedWhenReconciledSucceeded()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<CheckoutStateMachine, CheckoutSagaState>()
                    .InMemoryRepository();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var orderId = Guid.NewGuid();
        var paymentAttemptId = Guid.NewGuid();
        var sagaHarness = harness.GetSagaStateMachineHarness<CheckoutStateMachine, CheckoutSagaState>();

        await harness.Bus.Publish(CreateOrderSubmitted(orderId));
        (await sagaHarness.Exists(orderId, x => x.Reserving)).Should().Be(orderId);

        await harness.Bus.Publish(new InventoryReserved(orderId, [new ReservedItemDto("SKU-PRO-1", 1)], DateTime.UtcNow.AddMinutes(15)));
        (await sagaHarness.Exists(orderId, x => x.Paying)).Should().Be(orderId);

        // Timeout -> PaymentPendingResolution -> PaymentUnknown (NOT Cancelled!)
        await harness.Bus.Publish(new PaymentPendingResolution(orderId, paymentAttemptId, "Provider timeout"));
        (await sagaHarness.Exists(orderId, x => x.PaymentUnknown)).Should().Be(orderId);

        // Reconciled as Succeeded -> Committing
        await harness.Bus.Publish(new PaymentSucceeded(orderId, paymentAttemptId, "tx_recon_01", 1250.00m, "USD"));
        (await sagaHarness.Exists(orderId, x => x.Committing)).Should().Be(orderId);
        (await harness.Published.Any<CommitInventory>(x => x.Context.Message.OrderId == orderId)).Should().BeTrue();
    }

    [Fact]
    public async Task PaymentUnknown_WhenReservationExpiresBeforePaymentSucceeds_ShouldTriggerRefundPaymentInsteadOfCommit()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<CheckoutStateMachine, CheckoutSagaState>()
                    .InMemoryRepository();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var orderId = Guid.NewGuid();
        var paymentAttemptId = Guid.NewGuid();
        var sagaHarness = harness.GetSagaStateMachineHarness<CheckoutStateMachine, CheckoutSagaState>();

        await harness.Bus.Publish(CreateOrderSubmitted(orderId));
        (await sagaHarness.Exists(orderId, x => x.Reserving)).Should().Be(orderId);

        await harness.Bus.Publish(new InventoryReserved(orderId, [new ReservedItemDto("SKU-PRO-1", 1)], DateTime.UtcNow.AddMinutes(15)));
        (await sagaHarness.Exists(orderId, x => x.Paying)).Should().Be(orderId);

        await harness.Bus.Publish(new PaymentPendingResolution(orderId, paymentAttemptId, "Provider timeout"));
        (await sagaHarness.Exists(orderId, x => x.PaymentUnknown)).Should().Be(orderId);

        // Reservation expires while payment is still unknown
        await harness.Bus.Publish(new InventoryReservationExpired(Guid.NewGuid(), orderId, "SKU-PRO-1", 1, DateTime.UtcNow));

        // Now payment succeeds via late reconciliation -> must RefundPayment, NOT CommitInventory!
        await harness.Bus.Publish(new PaymentSucceeded(orderId, paymentAttemptId, "tx_late_recon", 1250.00m, "USD"));

        (await sagaHarness.Exists(orderId, x => x.Refunding)).Should().Be(orderId);
        (await harness.Published.Any<RefundPayment>(x =>
            x.Context.Message.OrderId == orderId &&
            x.Context.Message.PaymentAttemptId == paymentAttemptId)).Should().BeTrue();
    }

    [Fact]
    public async Task InventoryCommitFailed_ShouldTransitionToRefundingAndCancelAfterPaymentRefunded()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<CheckoutStateMachine, CheckoutSagaState>()
                    .InMemoryRepository();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var orderId = Guid.NewGuid();
        var paymentAttemptId = Guid.NewGuid();
        var sagaHarness = harness.GetSagaStateMachineHarness<CheckoutStateMachine, CheckoutSagaState>();

        await harness.Bus.Publish(CreateOrderSubmitted(orderId));
        (await sagaHarness.Exists(orderId, x => x.Reserving)).Should().Be(orderId);

        await harness.Bus.Publish(new InventoryReserved(orderId, [new ReservedItemDto("SKU-PRO-1", 1)], DateTime.UtcNow.AddMinutes(15)));
        (await sagaHarness.Exists(orderId, x => x.Paying)).Should().Be(orderId);

        await harness.Bus.Publish(new PaymentSucceeded(orderId, paymentAttemptId, "tx_01", 1250.00m, "USD"));
        (await sagaHarness.Exists(orderId, x => x.Committing)).Should().Be(orderId);

        // Commit fails (e.g., reservation expired at commit check)
        await harness.Bus.Publish(new InventoryCommitFailed(orderId, "Reservation expired before commit"));
        (await sagaHarness.Exists(orderId, x => x.Refunding)).Should().Be(orderId);
        (await harness.Published.Any<RefundPayment>(x => x.Context.Message.OrderId == orderId)).Should().BeTrue();

        // Refund completes
        await harness.Bus.Publish(new PaymentRefunded(orderId, paymentAttemptId, "rf_01", 1250.00m, "USD"));
        (await sagaHarness.Exists(orderId, x => x.Cancelled)).Should().Be(orderId);
        (await harness.Published.Any<OrderCancelled>(x => x.Context.Message.OrderId == orderId)).Should().BeTrue();
    }

    [Fact]
    public async Task LatePaymentSucceeded_OnAlreadyCancelledOrder_ShouldNotResurrectOrderAndShouldTriggerRefundPayment()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<CheckoutStateMachine, CheckoutSagaState>()
                    .InMemoryRepository();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var orderId = Guid.NewGuid();
        var lateAttemptId = Guid.NewGuid();
        var sagaHarness = harness.GetSagaStateMachineHarness<CheckoutStateMachine, CheckoutSagaState>();

        await harness.Bus.Publish(CreateOrderSubmitted(orderId));
        (await sagaHarness.Exists(orderId, x => x.Reserving)).Should().Be(orderId);

        await harness.Bus.Publish(new InventoryReserved(orderId, [new ReservedItemDto("SKU-PRO-1", 1)], DateTime.UtcNow.AddMinutes(15)));
        (await sagaHarness.Exists(orderId, x => x.Paying)).Should().Be(orderId);

        // Order gets cancelled due to reservation expiry during Paying
        await harness.Bus.Publish(new InventoryReservationExpired(Guid.NewGuid(), orderId, "SKU-PRO-1", 1, DateTime.UtcNow));
        (await sagaHarness.Exists(orderId, x => x.Cancelled)).Should().Be(orderId);

        // Late PaymentSucceeded arrives AFTER order is already Cancelled!
        await harness.Bus.Publish(new PaymentSucceeded(orderId, lateAttemptId, "tx_late_99", 1250.00m, "USD"));

        // Must NOT transition to Committing or Confirmed! Must transition to Refunding and publish RefundPayment!
        (await sagaHarness.Exists(orderId, x => x.Refunding)).Should().Be(orderId);
        (await harness.Published.Any<RefundPayment>(x =>
            x.Context.Message.OrderId == orderId &&
            x.Context.Message.PaymentAttemptId == lateAttemptId)).Should().BeTrue();
        (await harness.Published.Any<CommitInventory>(x => x.Context.Message.OrderId == orderId)).Should().BeFalse();
    }

    [Fact]
    public async Task LateInventoryReserved_OnAlreadyCancelledOrder_ShouldKeepCancelledAndPublishReleaseInventory()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddSagaStateMachine<CheckoutStateMachine, CheckoutSagaState>()
                    .InMemoryRepository();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        var orderId = Guid.NewGuid();
        var sagaHarness = harness.GetSagaStateMachineHarness<CheckoutStateMachine, CheckoutSagaState>();

        await harness.Bus.Publish(CreateOrderSubmitted(orderId));
        (await sagaHarness.Exists(orderId, x => x.Reserving)).Should().Be(orderId);

        await harness.Bus.Publish(new InventoryRejected(orderId, "Cancelled early", ["SKU-PRO-1"]));
        (await sagaHarness.Exists(orderId, x => x.Cancelled)).Should().Be(orderId);

        // Late InventoryReserved arrives after Cancelled
        await harness.Bus.Publish(new InventoryReserved(orderId, [new ReservedItemDto("SKU-PRO-1", 1)], DateTime.UtcNow.AddMinutes(15)));

        (await sagaHarness.Exists(orderId, x => x.Cancelled)).Should().Be(orderId);
        (await harness.Published.Any<ReleaseInventory>(x => x.Context.Message.OrderId == orderId)).Should().BeTrue();
        (await harness.Published.Any<CollectPayment>(x => x.Context.Message.OrderId == orderId)).Should().BeFalse();
    }
}

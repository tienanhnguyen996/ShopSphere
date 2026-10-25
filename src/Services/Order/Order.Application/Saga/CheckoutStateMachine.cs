using System.Text.Json;
using MassTransit;
using ShopSphere.BuildingBlocks.Messaging.Contracts;
using ShopSphere.Services.Inventory.Application.Contracts;
using ShopSphere.Services.Order.Application.Contracts;
using ShopSphere.Services.Payment.Application.Contracts;
using ShopSphere.Services.Shipping.Application.Contracts;

namespace ShopSphere.Services.Order.Application.Saga;

/// <summary>
/// MassTransit State Machine orchestrating the ShopSphere Checkout Saga.
/// Implements Happy Path, Failure Compensations, Timeout handling (PaymentUnknown),
/// and Late Event Protection (compensating late PaymentSucceeded / InventoryReserved on Cancelled orders).
/// </summary>
public class CheckoutStateMachine : MassTransitStateMachine<CheckoutSagaState>
{
    public static readonly TimeSpan DefaultReservationTtl = TimeSpan.FromMinutes(15);

    public State Reserving { get; private set; } = default!;
    public State Paying { get; private set; } = default!;
    public State PaymentUnknown { get; private set; } = default!;
    public State Committing { get; private set; } = default!;
    public State Shipping { get; private set; } = default!;
    public State Confirmed { get; private set; } = default!;
    public State Refunding { get; private set; } = default!;
    public State Cancelled { get; private set; } = default!;

    public Event<OrderSubmitted> OrderSubmittedEvent { get; private set; } = default!;
    public Event<InventoryReserved> InventoryReservedEvent { get; private set; } = default!;
    public Event<InventoryRejected> InventoryRejectedEvent { get; private set; } = default!;
    public Event<InventoryReservationExpired> InventoryReservationExpiredEvent { get; private set; } = default!;
    public Event<PaymentSucceeded> PaymentSucceededEvent { get; private set; } = default!;
    public Event<PaymentRejected> PaymentRejectedEvent { get; private set; } = default!;
    public Event<PaymentPendingResolution> PaymentPendingResolutionEvent { get; private set; } = default!;
    public Event<InventoryCommitted> InventoryCommittedEvent { get; private set; } = default!;
    public Event<InventoryCommitFailed> InventoryCommitFailedEvent { get; private set; } = default!;
    public Event<ShipmentCreated> ShipmentCreatedEvent { get; private set; } = default!;
    public Event<ShipmentFailed> ShipmentFailedEvent { get; private set; } = default!;
    public Event<PaymentRefunded> PaymentRefundedEvent { get; private set; } = default!;

    public CheckoutStateMachine()
    {
        InstanceState(x => x.CurrentState);

        Event(() => OrderSubmittedEvent, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => InventoryReservedEvent, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => InventoryRejectedEvent, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => InventoryReservationExpiredEvent, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentSucceededEvent, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentRejectedEvent, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentPendingResolutionEvent, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => InventoryCommittedEvent, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => InventoryCommitFailedEvent, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => ShipmentCreatedEvent, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => ShipmentFailedEvent, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentRefundedEvent, x => x.CorrelateById(m => m.Message.OrderId));

        // 1. Initial -> Reserving
        Initially(
            When(OrderSubmittedEvent)
                .Then(context =>
                {
                    context.Saga.CustomerId = context.Message.CustomerId;
                    context.Saga.TotalAmount = context.Message.TotalAmount;
                    context.Saga.Currency = context.Message.Currency;
                    context.Saga.PaymentMethod = context.Message.PaymentMethod;
                    context.Saga.ShippingAddressJson = JsonSerializer.Serialize(context.Message.ShippingAddress);
                    context.Saga.ItemsJson = JsonSerializer.Serialize(context.Message.Items);
                    context.Saga.CreatedAtUtc = DateTime.UtcNow;
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Reserving)
                .ThenAsync(async context =>
                {
                    var items = context.Message.Items
                        .Select(i => new InventoryItemRequirement(i.Sku, i.Quantity))
                        .ToList();

                    var command = new ReserveInventory(context.Saga.CorrelationId, items, DefaultReservationTtl);
                    await PublishWithEnvelopeAsync(context, command);
                }));

        // 2. Reserving -> Paying (or Cancelled on rejection)
        During(Reserving,
            When(InventoryReservedEvent)
                .Then(context =>
                {
                    context.Saga.ReservationExpiresAtUtc = context.Message.ExpiresAtUtc;
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Paying)
                .ThenAsync(async context =>
                {
                    var command = new CollectPayment(
                        context.Saga.CorrelationId,
                        context.Saga.TotalAmount,
                        context.Saga.Currency,
                        context.Saga.PaymentMethod);

                    await PublishWithEnvelopeAsync(context, command);
                }),

            When(InventoryRejectedEvent)
                .Then(context =>
                {
                    context.Saga.FailureReason = context.Message.Reason;
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Cancelled)
                .ThenAsync(async context =>
                {
                    var evt = new OrderCancelled(
                        context.Saga.CorrelationId,
                        context.Saga.CustomerId,
                        context.Message.Reason);

                    await PublishWithEnvelopeAsync(context, evt);
                }));

        // 3. Paying -> Committing / PaymentUnknown / Cancelled
        During(Paying,
            When(PaymentSucceededEvent)
                .Then(context =>
                {
                    context.Saga.PaymentAttemptId = context.Message.PaymentAttemptId;
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Committing)
                .ThenAsync(async context =>
                {
                    var command = new CommitInventory(context.Saga.CorrelationId);
                    await PublishWithEnvelopeAsync(context, command);
                }),

            When(PaymentRejectedEvent)
                .Then(context =>
                {
                    context.Saga.PaymentAttemptId = context.Message.PaymentAttemptId;
                    context.Saga.FailureReason = context.Message.Reason;
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Cancelled)
                .ThenAsync(async context =>
                {
                    var releaseCmd = new ReleaseInventory(context.Saga.CorrelationId, context.Message.Reason);
                    await PublishWithEnvelopeAsync(context, releaseCmd);

                    var cancelEvt = new OrderCancelled(
                        context.Saga.CorrelationId,
                        context.Saga.CustomerId,
                        context.Message.Reason);
                    await PublishWithEnvelopeAsync(context, cancelEvt);
                }),

            When(PaymentPendingResolutionEvent)
                .Then(context =>
                {
                    // Timeout = Unknown, NOT Failure!
                    context.Saga.PaymentAttemptId = context.Message.PaymentAttemptId;
                    context.Saga.FailureReason = context.Message.Reason;
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(PaymentUnknown),

            When(InventoryReservationExpiredEvent)
                .Then(context =>
                {
                    context.Saga.IsReservationExpired = true;
                    context.Saga.FailureReason = "Inventory reservation expired before payment completed.";
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Cancelled)
                .ThenAsync(async context =>
                {
                    var cancelEvt = new OrderCancelled(
                        context.Saga.CorrelationId,
                        context.Saga.CustomerId,
                        context.Saga.FailureReason!);
                    await PublishWithEnvelopeAsync(context, cancelEvt);
                }));

        // 4. PaymentUnknown -> Committing (or Refunding if hold expired!) / Cancelled
        During(PaymentUnknown,
            When(InventoryReservationExpiredEvent)
                .Then(context =>
                {
                    // Record that reservation expired while awaiting payment reconciliation
                    context.Saga.IsReservationExpired = true;
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                }),

            When(PaymentSucceededEvent)
                .IfElse(
                    context => context.Saga.IsReservationExpired,
                    expiredBinder => expiredBinder
                        .Then(context =>
                        {
                            context.Saga.PaymentAttemptId = context.Message.PaymentAttemptId;
                            context.Saga.FailureReason = "Payment succeeded after inventory reservation expired; refunding.";
                            context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                        })
                        .TransitionTo(Refunding)
                        .ThenAsync(async context =>
                        {
                            var refundCmd = new RefundPayment(
                                context.Saga.CorrelationId,
                                context.Message.PaymentAttemptId,
                                context.Saga.TotalAmount,
                                context.Saga.Currency,
                                context.Saga.FailureReason!);
                            await PublishWithEnvelopeAsync(context, refundCmd);
                        }),
                    activeBinder => activeBinder
                        .Then(context =>
                        {
                            context.Saga.PaymentAttemptId = context.Message.PaymentAttemptId;
                            context.Saga.FailureReason = null;
                            context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                        })
                        .TransitionTo(Committing)
                        .ThenAsync(async context =>
                        {
                            var commitCmd = new CommitInventory(context.Saga.CorrelationId);
                            await PublishWithEnvelopeAsync(context, commitCmd);
                        })),

            When(PaymentRejectedEvent)
                .Then(context =>
                {
                    context.Saga.PaymentAttemptId = context.Message.PaymentAttemptId;
                    context.Saga.FailureReason = context.Message.Reason;
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Cancelled)
                .ThenAsync(async context =>
                {
                    if (!context.Saga.IsReservationExpired)
                    {
                        var releaseCmd = new ReleaseInventory(context.Saga.CorrelationId, context.Message.Reason);
                        await PublishWithEnvelopeAsync(context, releaseCmd);
                    }

                    var cancelEvt = new OrderCancelled(
                        context.Saga.CorrelationId,
                        context.Saga.CustomerId,
                        context.Message.Reason);
                    await PublishWithEnvelopeAsync(context, cancelEvt);
                }));

        // 5. Committing -> Shipping / Refunding
        During(Committing,
            When(InventoryCommittedEvent)
                .Then(context =>
                {
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Shipping)
                .ThenAsync(async context =>
                {
                    var address = DeserializeShippingAddress(context.Saga.ShippingAddressJson);
                    var items = DeserializeOrderItems(context.Saga.ItemsJson)
                        .Select(i => new ShipmentItemDto(i.Sku, i.ProductName, i.Quantity))
                        .ToList();

                    var command = new CreateShipment(
                        context.Saga.CorrelationId,
                        context.Saga.CustomerId,
                        address,
                        items);

                    await PublishWithEnvelopeAsync(context, command);
                }),

            When(InventoryCommitFailedEvent)
                .Then(context =>
                {
                    context.Saga.FailureReason = context.Message.Reason;
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Refunding)
                .ThenAsync(async context =>
                {
                    var refundCmd = new RefundPayment(
                        context.Saga.CorrelationId,
                        context.Saga.PaymentAttemptId ?? Guid.Empty,
                        context.Saga.TotalAmount,
                        context.Saga.Currency,
                        context.Message.Reason);

                    await PublishWithEnvelopeAsync(context, refundCmd);
                }));

        // 6. Shipping -> Confirmed / Refunding
        During(Shipping,
            When(ShipmentCreatedEvent)
                .Then(context =>
                {
                    context.Saga.ShipmentId = context.Message.ShipmentId;
                    context.Saga.TrackingNumber = context.Message.TrackingNumber;
                    context.Saga.FailureReason = null;
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Confirmed)
                .ThenAsync(async context =>
                {
                    var evt = new OrderConfirmed(
                        context.Saga.CorrelationId,
                        context.Saga.CustomerId,
                        context.Saga.TotalAmount,
                        context.Saga.Currency,
                        context.Message.ShipmentId,
                        context.Message.TrackingNumber);

                    await PublishWithEnvelopeAsync(context, evt);
                }),

            When(ShipmentFailedEvent)
                .Then(context =>
                {
                    context.Saga.FailureReason = context.Message.Reason;
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Refunding)
                .ThenAsync(async context =>
                {
                    var refundCmd = new RefundPayment(
                        context.Saga.CorrelationId,
                        context.Saga.PaymentAttemptId ?? Guid.Empty,
                        context.Saga.TotalAmount,
                        context.Saga.Currency,
                        context.Message.Reason);

                    await PublishWithEnvelopeAsync(context, refundCmd);
                }));

        // 7. Refunding -> Cancelled
        During(Refunding,
            When(PaymentRefundedEvent)
                .Then(context =>
                {
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Cancelled)
                .ThenAsync(async context =>
                {
                    var cancelEvt = new OrderCancelled(
                        context.Saga.CorrelationId,
                        context.Saga.CustomerId,
                        context.Saga.FailureReason ?? "Order refunded and cancelled.");

                    await PublishWithEnvelopeAsync(context, cancelEvt);
                }),

            // Ignore duplicate events while refunding
            Ignore(InventoryReservationExpiredEvent));

        // 8. LATE EVENT PROTECTION on Cancelled state
        During(Cancelled,
            // A late PaymentSucceeded must NEVER resurrect a cancelled order!
            // Instead, open compensation by transitioning to Refunding and emitting RefundPayment.
            When(PaymentSucceededEvent)
                .Then(context =>
                {
                    context.Saga.PaymentAttemptId = context.Message.PaymentAttemptId;
                    context.Saga.FailureReason = "Late PaymentSucceeded arrived for already cancelled order; initiating refund compensation.";
                    context.Saga.UpdatedAtUtc = DateTime.UtcNow;
                })
                .TransitionTo(Refunding)
                .ThenAsync(async context =>
                {
                    var refundCmd = new RefundPayment(
                        context.Saga.CorrelationId,
                        context.Message.PaymentAttemptId,
                        context.Message.Amount,
                        context.Message.Currency,
                        context.Saga.FailureReason!);

                    await PublishWithEnvelopeAsync(context, refundCmd);
                }),

            // A late InventoryReserved on a cancelled order must release the held inventory immediately.
            When(InventoryReservedEvent)
                .ThenAsync(async context =>
                {
                    var releaseCmd = new ReleaseInventory(
                        context.Saga.CorrelationId,
                        "Late InventoryReserved arrived for already cancelled order.");

                    await PublishWithEnvelopeAsync(context, releaseCmd);
                }),

            Ignore(InventoryRejectedEvent),
            Ignore(PaymentRejectedEvent),
            Ignore(InventoryReservationExpiredEvent),
            Ignore(PaymentRefundedEvent));

        // 9. Ignore duplicate / late events on Confirmed state
        During(Confirmed,
            Ignore(InventoryReservedEvent),
            Ignore(PaymentSucceededEvent),
            Ignore(InventoryCommittedEvent),
            Ignore(ShipmentCreatedEvent),
            Ignore(InventoryReservationExpiredEvent));
    }

    private static async Task PublishWithEnvelopeAsync<TMessage>(
        BehaviorContext<CheckoutSagaState> context,
        TMessage payload) where TMessage : class
    {
        var envelope = MessageEnvelope<TMessage>.Create(
            payload,
            context.Saga.CorrelationId.ToString(),
            correlationId: context.Saga.CorrelationId);

        await context.Publish(envelope);
        await context.Publish(payload);
    }

    private static ShippingAddressDto DeserializeShippingAddress(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ShippingAddressDto("Customer", "Unknown", "Unknown", "", "00000", "US", "");
        }

        return JsonSerializer.Deserialize<ShippingAddressDto>(json)
            ?? new ShippingAddressDto("Customer", "Unknown", "Unknown", "", "00000", "US", "");
    }

    private static IReadOnlyList<OrderItemSnapshotDto> DeserializeOrderItems(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<OrderItemSnapshotDto>>(json) ?? [];
    }
}

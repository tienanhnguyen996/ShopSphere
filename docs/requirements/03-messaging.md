# Messaging Patterns and Contracts

## Message Contract Principles

Message names in this project (e.g., `ReserveInventory`, `InventoryReserved`, `PaymentSucceeded`) are **project-defined contracts**, not built-in MassTransit types.

Each message should include:
- `MessageId`: Unique identifier for this message instance
- `CorrelationId`: Links all messages in a workflow
- `CausationId`: The message that triggered this one
- `OccurredAtUtc`: When the business event occurred
- `ContractVersion`: Schema version for backward compatibility
- Business code/ID: Domain identifier (OrderId, PaymentId, etc.)

Propagate distributed trace context across HTTP, broker, and workers to connect all processing steps.

## Command vs. Event

**Commands** ask a service to perform an action. They have a single receiver and can be rejected.
- Examples: `ReserveInventory`, `CollectPayment`, `CreateShipment`, `CommitInventory`, `ReleaseInventory`

**Events** announce something that has happened. Multiple services can subscribe.
- Examples: `InventoryReserved`, `InventoryRejected`, `PaymentSucceeded`, `PaymentRejected`, `ShipmentCreated`, `OrderConfirmed`, `OrderCancelled`

## Saga Patterns

### Saga Orchestration

A central coordinator (the Saga instance) stores state, sends commands, and handles responses and timeouts.

**Use when**:
- One service naturally owns the workflow (e.g., OrderService owns checkout)
- You need explicit timeout handling
- Compensation logic is complex

**ShopSphere example**: The checkout Saga in OrderService orchestrates:
1. `ReserveInventory` → wait for response
2. `CollectPayment` → wait for response
3. `CommitInventory` → wait for response
4. `CreateShipment` → wait for response

The Saga stores:
- Current state (Reserving, Paying, Shipping, Completed, Failed)
- References (ReservationId, PaymentId, ShipmentId)
- Deadlines for each step

### Saga Choreography

Each service reacts to events and publishes new events. No central coordinator.

**Use when**:
- Steps are loosely coupled
- Each service knows its own next action
- No complex timeout or compensation needed

**Not used in core checkout** but could apply to notification fan-out or analytics updates.

## Outbox Pattern

### Why Outbox?

Writing to the database and publishing a message are two separate operations. If the app crashes between them, you get:
- **Lost message**: DB wrote, message never sent
- **Duplicate message**: Message sent, DB write rolled back, retry sends again

The Outbox pattern solves this: **Write business data and the pending message in the same transaction**. A background relay delivers the message after commit.

### MassTransit Outbox Options

**Bus Outbox** (API/request scope):
```csharp
services.AddDbContext<OrderDbContext>();
services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
    });
});
```

**Consumer Outbox** (message handler scope):
- Coordinates incoming message processing, business writes, and outgoing messages
- Prevents duplicate processing via Inbox pattern
- Use when a consumer writes to DB and publishes

### Implementation Rules

1. Business logic writes to the DB and calls `Publish`/`Send` in the same transaction
2. Outbox table stores pending messages
3. After commit, a background relay delivers messages to RabbitMQ
4. RabbitMQ confirms receipt
5. Relay marks messages as delivered

**Critical**: A RabbitMQ publisher confirm does NOT mean the consumer processed it. It only means the broker accepted it.

## Inbox Pattern (Consumer Deduplication)

When a consumer processes a message:
1. Check Inbox table for `MessageId`
2. If found, skip processing (already handled)
3. If not found, process message and write result + Inbox entry in one transaction

This prevents:
- Duplicate processing from broker redelivery
- Duplicate effects from network retries

**Business idempotency** is still needed for external side effects (e.g., calling a payment provider).

## Handling Failures

### Transient Errors

Retry with exponential backoff and a limit (e.g., 3 retries, max 10s delay).

**Examples**:
- Network timeout
- Database deadlock
- Temporary service unavailability

### Business Rejections

Do NOT retry. Follow the failure branch.

**Examples**:
- Out of stock (`InventoryRejected`)
- Payment declined (`PaymentRejected`)
- Invalid request (validation error)

### Unknown Results (Timeouts)

**Do not assume failure.** Store an operation reference and reconcile.

**Example**: Payment provider times out. The charge may have succeeded. Store the `PaymentAttemptId`, keep order in `PaymentUnknown` state, and query the provider before retrying.

### Late Events

A late event must not resurrect a cancelled transaction.

**Check**:
1. Current state/version of the aggregate
2. If already cancelled or completed, ignore or compensate
3. If side effects happened, open reconciliation

### Compensation

Compensation is a **new business operation**, not a rollback. It can fail.

**Examples**:
- Release inventory reservation
- Request refund from payment provider
- Cancel shipment (if not yet picked up)

Compensations need their own retry logic and may require manual intervention.

### Persistent Failures

Messages that fail repeatedly (after retries exhausted) go to an error queue. Requires:
- Monitoring and alerting
- Manual review
- Controlled replay after fix

## Concurrency Control

Use EF Core concurrency tokens (e.g., SQL Server `rowversion`) to detect conflicting updates:

```csharp
public class Reservation
{
    public Guid Id { get; set; }
    public int Quantity { get; set; }
    
    [Timestamp]
    public byte[] RowVersion { get; set; }
}
```

On conflict:
- EF Core throws `DbUpdateConcurrencyException`
- Application must handle (retry, reject, or escalate)
- Do NOT silently ignore

## Notification Independence

Failed notifications do NOT reverse completed business operations.

Notifications have their own:
- State (Pending, Sent, Failed)
- Retry logic
- Dead-letter handling

Checkout completes when payment and shipment succeed. Notification is a separate concern.

## References

- [MassTransit Outbox](https://masstransit.io/documentation/patterns/outbox)
- [MassTransit Sagas](https://masstransit.io/documentation/patterns/saga)
- [RabbitMQ Reliability](https://www.rabbitmq.com/reliability.html)
- [Azure Saga Pattern](https://learn.microsoft.com/en-us/azure/architecture/reference-architectures/saga/saga)
- [EF Core Concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)

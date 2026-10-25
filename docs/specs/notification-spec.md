# Notification Service Specification

> **Status**: Approved  
> **Service**: `NotificationService` (`src/Services/Notification`)  
> **Tasks**: `Task-54`, `Task-55`

## 1. Purpose & Service Boundaries
`NotificationService` is an asynchronous, non-blocking subscriber that listens to order and fulfillment lifecycle events (`OrderConfirmed`, `OrderCancelled`, `ShipmentHandedOver`, `ShipmentDelivered`) on its own dedicated RabbitMQ consumer queue and dispatches customer notifications (Email / SMS).

### Architectural Invariants
1. **Non-Blocking Subscriber (`02-services.md`)**: Notification failures **must never** block or roll back the Checkout Saga or order fulfillment. Notifications are processed asynchronously on an independent consumer queue with retry tracking.
2. **Idempotent Deduplication (`03-messaging.md`)**: Every notification job records `(OrderId, NotificationType, Channel)` and the triggering `MessageId`. Duplicate deliveries of `OrderConfirmed` or `OrderCancelled` are ignored if a notification of that type and channel has already been sent or queued for the order.
3. **Database Ownership**: Owns the `notification` schema in `ShopSphere_Notification` (`NotificationJobs` and MassTransit Outbox tables).

## 2. Domain Model (`Notification.Domain`)
### `NotificationChannel`
- `Email = 1`
- `Sms = 2`

### `NotificationType`
- `OrderConfirmed = 1`
- `OrderCancelled = 2`
- `ShipmentHandedOver = 3`
- `ShipmentDelivered = 4`

### `NotificationStatus`
- `Pending = 1`
- `Sent = 2`
- `Failed = 3`

### `NotificationJob` Aggregate Root
- `Id` (`Guid`)
- `OrderId` (`Guid`)
- `CustomerId` (`string`)
- `NotificationType` (`NotificationType`)
- `Channel` (`NotificationChannel`)
- `Recipient` (`string`)
- `Subject` (`string`)
- `Body` (`string`)
- `Status` (`NotificationStatus`)
- `SourceMessageId` (`Guid`)
- `RetryCount` (`int`)
- `ProviderReferenceId` (`string?`)
- `LastError` (`string?`)
- `CreatedAtUtc` (`DateTime`)
- `SentAtUtc` (`DateTime?`)
- `UpdatedAtUtc` (`DateTime?`)
- `RowVersion` (`byte[]`)
- Unique constraint on `(OrderId, NotificationType, Channel)` to guarantee at-most-once customer notification per lifecycle milestone and channel.

## 3. Message Contracts Consumed
- `ShopSphere.Services.Order.Application.Contracts.OrderConfirmed` (raw & `MessageEnvelope<OrderConfirmed>`)
- `ShopSphere.Services.Order.Application.Contracts.OrderCancelled` (raw & `MessageEnvelope<OrderCancelled>`)

## 4. HTTP API (`Notification.Api`)
- `GET /api/v1/notifications/orders/{orderId:guid}` — Returns all notification jobs for an order.
- `GET /api/v1/notifications/{id:guid}` — Returns a single notification job by ID.
- `POST /api/v1/notifications/{id:guid}/retry` — Manually retries a failed notification job.
- `GET /health` — SQL Server and RabbitMQ readiness check.

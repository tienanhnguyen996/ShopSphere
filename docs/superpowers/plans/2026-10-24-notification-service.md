# Notification Service Implementation Plan

> **For agentic workers:** Follow `docs/specs/notification-spec.md` and `AGENTS.md`.

## Goal
Implement the `NotificationService` (`Notification.Domain`, `Notification.Application`, `Notification.Infrastructure`, `Notification.Worker`, `Notification.Api`, and `tests/Notification.Tests.Unit`) covering Notion `Task-54` and `Task-55`.

## Tasks
1. **Task 1 (`Task-54`)**: Domain model (`NotificationChannel`, `NotificationType`, `NotificationStatus`, `NotificationJob` aggregate root with retry & status transitions) and `NotificationDbContext` (`notification` schema, unique index on `(OrderId, NotificationType, Channel)`).
2. **Task 2 (`Task-55`)**: `OrderNotificationConsumer` consuming `OrderConfirmed` and `OrderCancelled` (both raw and `MessageEnvelope<T>`), deduplicating by `(OrderId, NotificationType, Channel)`, dispatching via `INotificationSender`, and catching provider errors into `NotificationStatus.Failed` (`RecordAttemptFailure`) so checkout/saga is never blocked.
3. **Task 3 (`Task-54`, `Task-55`)**: HTTP endpoints (`GET /api/v1/notifications/orders/{orderId:guid}`, `GET /api/v1/notifications/{id:guid}`, `POST /api/v1/notifications/{id:guid}/retry`), OpenAPI specification (`docs/specs/notification-openapi.json`), EF Core migration (`InitialNotificationMigration`), and unit tests in `tests/Notification.Tests.Unit/`.

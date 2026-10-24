# Shipping Service Specification

## 1. Overview & Service Boundaries
The Shipping Service (`ShopSphere.Services.Shipping`) owns order fulfillment, carrier label creation, tracking milestones, and physical carrier handover.

- **Boundary**: Sole authority for `Shipment` aggregates and `TrackingMilestone` history in SQL Server (`ShopSphereShipping`, schema `shipping`).
- **Idempotency**: Enforces a unique constraint on `OrderId` so duplicate `CreateShipment` commands never create two shipments for one order.
- **Timeout = Unknown, not Failure**: External carrier API timeouts transition the shipment to `ShipmentUnknown` with the `ExternalShipmentRequestId` stored for reconciliation rather than blindly re-booking labels.
- **Physical Handover Guard**: Once a shipment reaches `HandedOver`, `InTransit`, or `Delivered`, it **cannot** be cancelled by rolling back a database flag; a returns workflow is required.
- **Physical Stock-Out Trigger**: Recording carrier handover emits `ShipmentHandedOver` via the Transactional Outbox so `InventoryService` records physical stock-out idempotently.

---

## 2. Domain Model (`Shipping.Domain`)

### 2.1 ShipmentStatus Enum
- `Pending` (0): Initial fulfillment request received.
- `LabelCreated` (1): Carrier label booked and `TrackingNumber` assigned.
- `ShipmentUnknown` (2): Carrier API timed out; awaiting status inquiry using `ExternalShipmentRequestId`.
- `HandedOver` (3): Physical package handed over to carrier (`HandedOverAtUtc` recorded).
- `InTransit` (4): Package moving through carrier network.
- `Delivered` (5): Package delivered to recipient (`DeliveredAtUtc` recorded).
- `Cancelled` (6): Cancelled prior to physical carrier handover.
- `Failed` (7): Unrecoverable carrier rejection.

### 2.2 Shipment Aggregate Root
- `Id` (`Guid`)
- `OrderId` (`Guid`, unique index)
- `CustomerId` (`string`)
- `Carrier` (`string`)
- `TrackingNumber` (`string?`, indexed)
- `ExternalShipmentRequestId` (`string?`)
- `Status` (`ShipmentStatus`)
- `Address` (`ShippingAddress` value object)
- `Items` (`IReadOnlyCollection<ShipmentItem>`)
- `Milestones` (`IReadOnlyCollection<TrackingMilestone>`)
- `HandedOverAtUtc` (`DateTime?`)
- `DeliveredAtUtc` (`DateTime?`)
- `FailureReason` (`string?`)
- `RowVersion` (`byte[]` concurrency token)
- `CreatedAtUtc` (`DateTime`)
- `UpdatedAtUtc` (`DateTime?`)

---

## 3. Message Contracts (`ShopSphere.Services.Shipping.Application.Contracts`)

### 3.1 Consumed Commands
- `CreateShipment(Guid OrderId, string CustomerId, ShippingAddressDto Address, IReadOnlyList<ShipmentItemDto> Items)`

### 3.2 Published Events
- `ShipmentCreated(Guid OrderId, Guid ShipmentId, string TrackingNumber)`
- `ShipmentFailed(Guid OrderId, string Reason)`
- `ShipmentHandedOver(Guid ShipmentId, Guid OrderId, string TrackingNumber, IReadOnlyList<ShipmentItemDto> Items, DateTime HandedOverAtUtc)`
- `ShipmentDelivered(Guid ShipmentId, Guid OrderId, string TrackingNumber, DateTime DeliveredAtUtc)`

---

## 4. HTTP API Endpoints (`Shipping.Api`)

| Method | Route | Description | Status Codes |
|---|---|---|---|
| `GET` | `/api/v1/shipping/orders/{orderId:guid}` | Get shipment & tracking timeline by Order ID | `200 OK`, `404 Not Found` |
| `GET` | `/api/v1/shipping/{id:guid}` | Get shipment & tracking timeline by Shipment ID | `200 OK`, `404 Not Found` |
| `GET` | `/api/v1/shipping/track/{trackingNumber}` | Get shipment & tracking timeline by Tracking Number | `200 OK`, `404 Not Found` |
| `POST` | `/api/v1/shipping/{id:guid}/handover` | Record physical carrier handover & emit `ShipmentHandedOver` | `200 OK`, `404 Not Found`, `409 Conflict` |
| `POST` | `/api/v1/shipping/{id:guid}/deliver` | Record delivery milestone & emit `ShipmentDelivered` | `200 OK`, `404 Not Found`, `409 Conflict` |
| `POST` | `/api/v1/shipping/{id:guid}/cancel` | Cancel shipment (rejected if already handed over) | `200 OK`, `404 Not Found`, `409 Conflict` |
| `GET` | `/health` | Health check for SQL Server and RabbitMQ | `200 OK`, `503 Service Unavailable` |

# Order Service Specification

## 1. Overview & Service Boundaries
The Order Service (`ShopSphere.Services.Order`) is the central orchestrator of the ShopSphere checkout lifecycle. It owns order aggregates, immutable price snapshots, persistent checkout idempotency records, and the **MassTransit Checkout State Machine Saga**.

- **Boundary**: Sole authority for customer orders, checkout price snapshots, and checkout saga orchestration. Never accesses another service's database tables directly.
- **Source of Truth**: SQL Server (`ShopSphereOrders` database, schema `order`) with EF Core optimistic concurrency tokens (`RowVersion`). Order state is **NEVER** cached as authoritative truth in Redis.
- **Transactional Outbox**: Order creation, state transitions, saga state updates, and outgoing commands/events are committed atomically within the same SQL transaction via MassTransit's Entity Framework Outbox.
- **Idempotency**: Checkout requests require an `Idempotency-Key` header. The key, SHA-256 request fingerprint, operation code, and serialized response are persisted in `order.IdempotencyRecords`. Replaying the same key + identical payload returns the original `OrderId` and response; replaying the same key with a different payload returns `409 Conflict` (`Idempotency.PayloadMismatch`).
- **Timeout = Unknown, not Failure**: When `PaymentPendingResolution` is received, the order transitions to `PaymentUnknown` (never automatically failed or double-charged) while awaiting provider reconciliation or webhook resolution.
- **Late Event Protection**: Every consumer and saga transition validates the current aggregate/saga state before applying an event. A late `PaymentSucceeded` arriving for an already `Cancelled` order **never** resurrects the order; instead, it triggers a compensating `RefundPayment` command and transitions to `Refunding`.

```
+-----------------------------------------------------------------------------------+
|                        Client / API Gateway (HTTP)                                |
+-----------------------------------------------------------------------------------+
       | POST /api/v1/orders/checkout (Idempotency-Key + X-Customer-Id)
       v
+-----------------------------------------------------------------------------------+
|                               Order Service API                                   |
|  1. Verify Idempotency-Key & SHA-256 Request Fingerprint (SQL Idempotency Store)  |
|  2. Fetch & Verify Live Cart Quote via HTTP (CartService -> CatalogService)       |
|  3. Persist Order Snapshot (Pending) + Publish OrderSubmitted in SQL Outbox       |
+-----------------------------------------------------------------------------------+
       | OrderSubmitted
       v
+-----------------------------------------------------------------------------------+
|                  MassTransit Checkout State Machine Saga                          |
|  - Reserving  --> ReserveInventory   --> InventoryReserved / InventoryRejected    |
|  - Paying     --> CollectPayment     --> PaymentSucceeded / Rejected / Unknown    |
|  - Committing --> CommitInventory    --> InventoryCommitted / CommitFailed        |
|  - Shipping   --> CreateShipment     --> ShipmentCreated / ShipmentFailed         |
|  - Terminal   --> Confirmed / Cancelled / Refunding (with Late Event Compensation)|
+-----------------------------------------------------------------------------------+
```

---

## 2. Domain Model & Invariants (`Order.Domain`)

### 2.1 OrderStatus Enum (All 7 States)
- `Pending` (0): Initial order created with verified price snapshot, awaiting saga start.
- `Reserving` (1): Inventory reservation requested (`ReserveInventory` sent).
- `Paying` (2): Stock held (`InventoryReserved`); payment charge initiated (`CollectPayment` sent).
- `PaymentUnknown` (3): Payment provider timed out or returned ambiguous status (`PaymentPendingResolution`). Awaiting reconciliation.
- `Confirmed` (4): Payment succeeded, stock committed, and shipment created (`ShipmentCreated`).
- `Refunding` (5): Payment succeeded on an expired hold, failed commit/shipment, or cancelled order; refund initiated (`RefundPayment` sent).
- `Cancelled` (6): Terminal cancelled state (due to out-of-stock, declined payment, expired hold, customer cancellation, or completed refund).

### 2.2 Order Aggregate Root
- `Id` (`Guid`): Primary key (`OrderId`).
- `CustomerId` (`string`): Customer identifier.
- `Items` (`IReadOnlyCollection<OrderItem>`): Immutable snapshot of purchased line items.
- `ShippingAddress` (`ShippingAddress`): Value object (`RecipientName`, `Street`, `City`, `State`, `PostalCode`, `Country`, `Phone`).
- `TotalAmount` (`decimal`): Confirmed order total (`sum(item.LineTotal)`).
- `Currency` (`string`): 3-character uppercase ISO currency code (e.g., `USD`).
- `PaymentMethod` (`string`): Selected payment method (e.g., `credit_card`, `mock`).
- `Status` (`OrderStatus`): Current order lifecycle state.
- `PaymentAttemptId` (`Guid?`): Linked payment attempt reference.
- `ShipmentId` (`Guid?`): Linked shipment reference.
- `TrackingNumber` (`string?`): Carrier tracking code once shipment is created.
- `FailureReason` (`string?`): Business rejection or failure reason.
- `CancellationReason` (`string?`): Reason for cancellation.
- `RowVersion` (`byte[]`): SQL Server `rowversion` concurrency token.
- `CreatedAtUtc` (`DateTime`)
- `LastModifiedUtc` (`DateTime`)

#### State Transition Rules & Invariants
- `Create(Guid id, string customerId, ShippingAddress address, string paymentMethod, IEnumerable<OrderItem> items)`:
  - Requires non-empty `customerId`, valid `address`, non-empty `paymentMethod`, and at least 1 `OrderItem`.
  - Computes `TotalAmount` from item snapshots (`Quantity * UnitPrice`).
  - Initializes `Status = OrderStatus.Pending`.
- `MarkReserving()`:
  - Valid from `Pending` (idempotent if already `Reserving`).
  - Rejects if `Cancelled`, `Confirmed`, or `Refunding`.
- `MarkPaying()`:
  - Valid from `Pending` or `Reserving` (idempotent if already `Paying`).
  - Rejects if `Cancelled`, `Confirmed`, or `Refunding`.
- `MarkPaymentUnknown(Guid paymentAttemptId, string reason)`:
  - Valid from `Paying` or `PaymentUnknown`.
  - Rejects if in terminal state (`Confirmed`, `Cancelled`, `Refunding`).
  - Stores `PaymentAttemptId` and `FailureReason = reason`.
- `MarkPaymentConfirmed(Guid paymentAttemptId)`:
  - Valid from `Paying` or `PaymentUnknown`.
  - Stores `PaymentAttemptId`.
- `MarkConfirmed(Guid? shipmentId = null, string? trackingNumber = null)`:
  - Valid from `Paying`, `PaymentUnknown`, or `Confirmed` (idempotent).
  - Rejects if `Cancelled` or `Refunding`.
- `MarkRefunding(string reason, Guid? paymentAttemptId = null)`:
  - Valid from `Paying`, `PaymentUnknown`, `Confirmed`, or `Cancelled` (when a late payment arrives after cancellation).
  - Sets `Status = OrderStatus.Refunding`, `CancellationReason = reason`.
- `MarkCancelled(string reason)`:
  - Valid from `Pending`, `Reserving`, `Paying`, `PaymentUnknown`, or `Refunding` (idempotent if already `Cancelled`).
  - Rejects if `Confirmed` (confirmed orders with active shipments require cancellation/refund check).

### 2.3 OrderItem Entity (Price Snapshot)
- `Id` (`Guid`)
- `OrderId` (`Guid`)
- `Sku` (`string`)
- `ProductName` (`string`)
- `Quantity` (`int`, `> 0`)
- `UnitPrice` (`decimal`, `>= 0`)
- `LineTotal` (`decimal`, `Quantity * UnitPrice`)
- `Currency` (`string`)
- `PriceVersion` (`int`): Catalog price version captured at checkout.

---

## 3. Message Contracts & Saga State Machine (`Order.Application`)

### 3.1 Commands Emitted by Checkout Saga
- `ReserveInventory(Guid OrderId, IReadOnlyList<InventoryItemRequirement> Items, TimeSpan ReservationTtl)`
- `ReleaseInventory(Guid OrderId, string Reason)`
- `CommitInventory(Guid OrderId)`
- `CollectPayment(Guid OrderId, decimal Amount, string Currency, string PaymentMethod)`
- `RefundPayment(Guid OrderId, Guid PaymentAttemptId, decimal Amount, string Currency, string Reason)`
- `CreateShipment(Guid OrderId, string CustomerId, ShippingAddressDto Address, IReadOnlyList<ShipmentItemDto> Items)`

### 3.2 Events Consumed by Checkout Saga
- `OrderSubmitted(Guid OrderId, string CustomerId, decimal TotalAmount, string Currency, string PaymentMethod, ShippingAddressDto ShippingAddress, IReadOnlyList<OrderItemSnapshotDto> Items)`
- `InventoryReserved(Guid OrderId, IReadOnlyList<ReservedItemDto> Items, DateTime ExpiresAtUtc)`
- `InventoryRejected(Guid OrderId, string Reason, IReadOnlyList<string> UnavailableSkus)`
- `InventoryCommitted(Guid OrderId, IReadOnlyList<ReservedItemDto> Items)`
- `InventoryCommitFailed(Guid OrderId, string Reason)`
- `InventoryReservationExpired(Guid ReservationId, Guid OrderId, string Sku, int Quantity, DateTime ExpiredAtUtc)`
- `PaymentSucceeded(Guid OrderId, Guid PaymentAttemptId, string ProviderTransactionId, decimal Amount, string Currency)`
- `PaymentRejected(Guid OrderId, Guid PaymentAttemptId, string Reason)`
- `PaymentPendingResolution(Guid OrderId, Guid PaymentAttemptId, string Reason)`
- `PaymentRefunded(Guid OrderId, Guid PaymentAttemptId, string RefundTransactionId, decimal Amount, string Currency)`
- `ShipmentCreated(Guid OrderId, Guid ShipmentId, string TrackingNumber)`
- `ShipmentFailed(Guid OrderId, string Reason)`

### 3.3 Events Published by Order / Saga
- `OrderConfirmed(Guid OrderId, string CustomerId, decimal TotalAmount, string Currency, Guid ShipmentId, string TrackingNumber)`
- `OrderCancelled(Guid OrderId, string CustomerId, string Reason)`

### 3.4 Saga States & Transitions (`CheckoutStateMachine`)
| Current State | Trigger Event | Next State | Actions / Side Effects |
|---|---|---|---|
| `Initial` | `OrderSubmitted` | `Reserving` | Update Order to `Reserving`; publish `ReserveInventory` (TTL: 15m). |
| `Reserving` | `InventoryReserved` | `Paying` | Store `ReservationExpiresAtUtc`; update Order to `Paying`; publish `CollectPayment`. |
| `Reserving` | `InventoryRejected` | `Cancelled` | Update Order to `Cancelled`; publish `OrderCancelled`. |
| `Paying` | `PaymentSucceeded` | `Committing` | Store `PaymentAttemptId`; publish `CommitInventory`. |
| `Paying` | `PaymentRejected` | `Cancelled` | Update Order to `Cancelled`; publish `ReleaseInventory` + `OrderCancelled`. |
| `Paying` | `PaymentPendingResolution` | `PaymentUnknown` | Store `PaymentAttemptId`; update Order to `PaymentUnknown`. |
| `Paying` | `InventoryReservationExpired` | `Cancelled` | Update Order to `Cancelled` (`Reservation expired`); publish `OrderCancelled`. |
| `PaymentUnknown` | `PaymentSucceeded` | `Committing` | Store `PaymentAttemptId`; publish `CommitInventory`. |
| `PaymentUnknown` | `PaymentRejected` | `Cancelled` | Update Order to `Cancelled`; publish `ReleaseInventory` + `OrderCancelled`. |
| `PaymentUnknown` | `InventoryReservationExpired` | `PaymentUnknown` | Record `IsReservationExpired = true`; await payment resolution (if payment later succeeds, trigger `RefundPayment`!). |
| `Committing` | `InventoryCommitted` | `Shipping` | Publish `CreateShipment`. |
| `Committing` | `InventoryCommitFailed` | `Refunding` | Update Order to `Refunding`; publish `RefundPayment`. |
| `Shipping` | `ShipmentCreated` | `Confirmed` | Store `ShipmentId` & `TrackingNumber`; update Order to `Confirmed`; publish `OrderConfirmed`. |
| `Shipping` | `ShipmentFailed` | `Refunding` | Update Order to `Refunding`; publish `RefundPayment`. |
| `Refunding` | `PaymentRefunded` | `Cancelled` | Update Order to `Cancelled`; publish `OrderCancelled`. |
| `Cancelled` | **Late** `PaymentSucceeded` | `Refunding` | **Late Event Protection**: Do NOT resurrect order! Transition to `Refunding` and publish `RefundPayment`. |
| `Cancelled` | **Late** `InventoryReserved` | `Cancelled` | **Late Event Protection**: Keep `Cancelled` and publish `ReleaseInventory` compensation. |

---

## 4. Persistence & Concurrency (`Order.Infrastructure`)

### 4.1 Database Schema (`order`)
- `order.Orders`: PK `Id`, indexed by `CustomerId`, `Status`, `CreatedAtUtc`; `RowVersion` (`rowversion`).
- `order.OrderItems`: PK `Id`, FK `OrderId` (`Cascade`).
- `order.CheckoutSagaStates`: PK `CorrelationId` (`OrderId`), `CurrentState`, `CustomerId`, `TotalAmount`, `Currency`, `PaymentMethod`, `PaymentAttemptId`, `ShipmentId`, `TrackingNumber`, `ReservationExpiresAtUtc`, `IsReservationExpired`, `FailureReason`, `RowVersion` (`rowversion`).
- `order.IdempotencyRecords`: PK `Key` (`nvarchar(128)`), `RequestHash` (`nvarchar(128)`), `OperationCode` (`nvarchar(100)`), `StatusCode` (`int`), `ResponseBody` (`nvarchar(max)`), `CreatedAtUtc`, `ExpiresAtUtc`.
- MassTransit Outbox tables (`InboxState`, `OutboxMessage`, `OutboxState`).

---

## 5. HTTP API Endpoints (`Order.Api`)

| Method | Route | Auth / Headers | Description | Status Codes |
|---|---|---|---|---|
| `POST` | `/api/v1/orders/checkout` | `Idempotency-Key` (required)<br>`X-Customer-Id` or JWT `sub` | Verify cart quote, create order snapshot, start Checkout Saga | `201 Created`, `200 OK` (replay), `400 Bad Request`, `409 Conflict`, `503 Service Unavailable` |
| `GET` | `/api/v1/orders/{id:guid}` | Authenticated / `X-Customer-Id` | Get order details, items, status, and tracking info | `200 OK`, `404 Not Found` |
| `GET` | `/api/v1/orders` | Authenticated / `X-Customer-Id` | List orders for customer (paged) | `200 OK` |
| `POST` | `/api/v1/orders/{id:guid}/cancel` | Authenticated / `X-Customer-Id` | Request order cancellation | `200 OK`, `404 Not Found`, `409 Conflict` |
| `GET` | `/health` | Anonymous | Health check for SQL Server, RabbitMQ, and Cart Service | `200 OK`, `503 Service Unavailable` |

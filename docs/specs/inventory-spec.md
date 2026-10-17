# Inventory Service Specification

## 1. Overview & Service Boundaries
The Inventory Service manages product stock allocations, reservation holds, stock commitments, and inventory intake adjustments.

- **Boundary**: The Inventory Service is the sole authority for stock levels. Stock counts and reservations are **NEVER** cached or stored in Redis.
- **Source of Truth**: SQL Server (`ShopSphere.Inventory` database, schema `inventory`) with EF Core optimistic concurrency tokens (`RowVersion`).
- **Transactional Outbox**: All state mutations and outgoing domain/integration events are saved in the same SQL transaction via MassTransit's transactional outbox.
- **Expiry Model**: Reservation expiry is a verified state transition inside the commit transaction (`ExpiresAtUtc > UtcNow`). A background worker (`ReservationExpiryWorker`) also sweeps overdue holds.

```
+-----------------------------------------------------------------------------------+
|                                  Order / Saga                                     |
+-----------------------------------------------------------------------------------+
       | ReserveInventory               | CommitInventory               | ReleaseInventory
       v                                v                               v
+-----------------------------------------------------------------------------------+
|                        Inventory Service (RabbitMQ Consumers)                     |
+-----------------------------------------------------------------------------------+
       |                                |                               |
       | HoldStock                      | CommitHold                    | ReleaseHold
       v                                v                               v
+-----------------------------------------------------------------------------------+
|             SQL Server (inventory.StockItems, inventory.Reservations)             |
|              Concurrency Token (RowVersion) + Transactional Outbox                |
+-----------------------------------------------------------------------------------+
       |                                |                               |
       | InventoryReserved / Rejected   | InventoryCommitted / Failed   | InventoryReleased
       v                                v                               v
+-----------------------------------------------------------------------------------+
|                                 RabbitMQ Broker                                   |
+-----------------------------------------------------------------------------------+
```

---

## 2. Domain Model & Invariants (`Inventory.Domain`)

### 2.1 StockItem Aggregate
- `Id` (`Guid`): Primary key.
- `Sku` (`string`): Unique alphanumeric product SKU (`^[A-Z0-9-_]+$`), max 50 chars.
- `QuantityAvailable` (`int`): Free stock that can be held. Invariant: `QuantityAvailable >= 0`.
- `QuantityHeld` (`int`): Stock held for pending orders. Invariant: `QuantityHeld >= 0`.
- `QuantityCommitted` (`int`): Stock confirmed after payment awaiting fulfillment. Invariant: `QuantityCommitted >= 0`.
- `RowVersion` (`byte[]`): SQL Server timestamp/rowversion concurrency token.
- `CreatedAtUtc` (`DateTime`)
- `LastModifiedUtc` (`DateTime`)

#### Invariants & Methods
- `HoldStock(Guid orderId, int quantity, TimeSpan ttl)`:
  - If `quantity <= 0`, throws `ArgumentOutOfRangeException`.
  - If `QuantityAvailable < quantity`, throws `InsufficientStockDomainException(Sku, quantity, QuantityAvailable)`.
  - Mutates buckets:
    - `QuantityAvailable -= quantity`
    - `QuantityHeld += quantity`
  - Creates and attaches a `StockReservation` in `ReservationStatus.Held` with `ExpiresAtUtc = DateTime.UtcNow.Add(ttl)`.
  - Returns the created `StockReservation`.
- `CommitHold(Guid reservationId, DateTime utcNow)`:
  - Locates reservation for this aggregate.
  - Verifies reservation is in `Held` state. If already `Committed`, idempotent return.
  - Verifies `reservation.ExpiresAtUtc > utcNow`. If expired, transitions reservation to `ReservationStatus.Expired`, releases held stock back to available, and throws `ReservationExpiredDomainException`.
  - Mutates buckets:
    - `QuantityHeld -= reservation.Quantity`
    - `QuantityCommitted += reservation.Quantity`
  - Transitions reservation status to `ReservationStatus.Committed`.
- `ReleaseHold(Guid reservationId, string reason)`:
  - Locates reservation.
  - If already `Released` or `Expired`, idempotent no-op.
  - If `Committed`, throws `InvalidOperationException("Cannot release committed stock")`.
  - Mutates buckets:
    - `QuantityHeld -= reservation.Quantity`
    - `QuantityAvailable += reservation.Quantity`
  - Transitions reservation to `ReservationStatus.Released`.
- `AdjustStock(int adjustmentQuantity, string reason)`:
  - Adds `adjustmentQuantity` to `QuantityAvailable`.
  - Invariant: `QuantityAvailable + adjustmentQuantity >= 0`. If negative, throws `InvalidOperationException("Adjustment would result in negative available quantity")`.

### 2.2 StockReservation Entity
- `Id` (`Guid`): Unique identifier.
- `OrderId` (`Guid`): Order that holds this stock.
- `StockItemId` (`Guid`): Reference to `StockItem`.
- `Sku` (`string`): Product SKU snapshot.
- `Quantity` (`int`): Number of items reserved (`> 0`).
- `Status` (`ReservationStatus`): `Held`, `Committed`, `Released`, `Expired`.
- `ExpiresAtUtc` (`DateTime`): Expiry timestamp for the hold.
- `CreatedAtUtc` (`DateTime`)
- `LastModifiedUtc` (`DateTime`)

### 2.3 Domain Exceptions
- `InsufficientStockDomainException(string sku, int requested, int available)`
- `ReservationExpiredDomainException(Guid reservationId, Guid orderId, DateTime expiredAtUtc)`

---

## 3. Persistence & Concurrency (`Inventory.Infrastructure`)

### 3.1 Entity Framework Core Configuration
- Database context: `InventoryDbContext` in schema `inventory`.
- Tables:
  - `inventory.StockItems`
    - Primary Key: `Id`
    - Alternate Unique Key: `Sku`
    - Concurrency Token: `builder.Property(x => x.RowVersion).IsRowVersion();`
  - `inventory.Reservations`
    - Primary Key: `Id`
    - Index: Unique on `(OrderId, Sku)`
    - Index: Index on `(Status, ExpiresAtUtc)` for fast worker querying
  - MassTransit Outbox tables (`inventory.OutboxMessage`, `inventory.OutboxState`)

### 3.2 Concurrency Handling
When two concurrent requests attempt to reserve or modify the same `StockItem`:
1. EF Core throws `DbUpdateConcurrencyException`.
2. The consumer catches the exception and reloads the entity with a backoff retry policy (up to 3 retries).
3. If stock is no longer sufficient on retry, publishes `InventoryRejected`.

---

## 4. Message Contracts & Workflows (`Inventory.Application`)

All messages are wrapped inside `MessageEnvelope<T>` from `ShopSphere.BuildingBlocks.Messaging.Contracts`.

### 4.1 Commands & Events

#### ReserveInventory
- **Command**: `ReserveInventory(Guid OrderId, IReadOnlyList<InventoryItemRequirement> Items, TimeSpan ReservationTtl)`
- **Success Event**: `InventoryReserved(Guid OrderId, IReadOnlyList<ReservedItemDto> Items, DateTime ExpiresAtUtc)`
- **Rejection Event**: `InventoryRejected(Guid OrderId, string Reason, IReadOnlyList<string> UnavailableSkus)`

#### ReleaseInventory
- **Command**: `ReleaseInventory(Guid OrderId, string Reason)`
- **Event**: `InventoryReleased(Guid OrderId, string Reason)`

#### CommitInventory
- **Command**: `CommitInventory(Guid OrderId)`
- **Success Event**: `InventoryCommitted(Guid OrderId, IReadOnlyList<ReservedItemDto> Items)`
- **Failure Event**: `InventoryCommitFailed(Guid OrderId, string Reason)`

#### InventoryReservationExpired
- **Event**: `InventoryReservationExpired(Guid ReservationId, Guid OrderId, string Sku, int Quantity, DateTime ExpiredAtUtc)`

---

## 5. Background Services (`Inventory.Worker`)

### 5.1 ReservationExpiryWorker (`BackgroundService`)
- Runs on a periodic timer (default: 30 seconds).
- Fetches batches of reservations where `Status == ReservationStatus.Held` and `ExpiresAtUtc <= DateTime.UtcNow`.
- For each expired reservation:
  - Opens a transaction.
  - Loads associated `StockItem`.
  - Restores stock: `QuantityHeld -= reservation.Quantity`, `QuantityAvailable += reservation.Quantity`.
  - Marks reservation as `ReservationStatus.Expired`.
  - Writes `InventoryReservationExpired` outbox message.
  - Commits transaction.

---

## 6. HTTP API Endpoints (`Inventory.Api`)

- `POST /api/v1/inventory/adjustments`: Stock adjustment endpoint for warehouse intake/corrections.
  - Request: `{ "sku": "ELEC-0001", "quantity": 50, "reason": "Restock batch #42" }`
  - Response: `200 OK` with updated stock state, or `400 Bad Request` if adjustment exceeds available stock.
- `GET /api/v1/inventory/{sku}`: Query current stock breakdown (`quantityAvailable`, `quantityHeld`, `quantityCommitted`).
- `GET /health`: Health check probing SQL Server and RabbitMQ connectivity.

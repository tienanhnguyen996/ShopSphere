# ShopSphere System Architecture

This document provides the complete architectural reference for the **ShopSphere** distributed e-commerce platform, including service topology, the MassTransit Checkout Saga state machine, Transactional Outbox messaging flows, carrier fulfillment lifecycle, and database ownership boundaries.

![ShopSphere System Architecture](./assets/system-architecture.svg)

---

## 1. High-Level System Topology

All browser traffic enters exclusively through the **YARP Reverse Proxy & BFF Gateway (`:5000`)**. Synchronous cross-service reads (such as `Cart.Api` verifying live prices from `Catalog.Api`, or `Order.Api` fetching the verified checkout quote from `Cart.Api`) occur over HTTP, while all multi-service checkout, inventory, payment, shipping, and notification workflows are coordinated asynchronously via **RabbitMQ** and **MassTransit Transactional Outbox** workers.

```mermaid
flowchart TB
    subgraph Client["Client Tier"]
        SPA["React 18 + TypeScript SPA (:3000)\nCustomer Storefront & Staff Admin Portal"]
    end

    subgraph Edge["API Gateway / BFF Tier"]
        YARP["ShopSphere.Gateway (:5000)\nYARP Reverse Proxy • Partitioned Rate Limiter\nW3C Trace & X-Correlation-Id • Security Headers"]
    end

    subgraph Services["Microservices & Outbox Workers (.NET 10)"]
        ID["Identity.Api (:5007)\nJWT Auth & RBAC"]
        CAT["Catalog.Api (:5001)\n+ Catalog.Worker"]
        CART["Cart.Api (:5002)\nQuote Verification"]
        ORD["Order.Api (:5005)\n+ Order.Worker (CheckoutStateMachine)"]
        INV["Inventory.Api (:5003)\n+ Inventory.Worker (ReservationExpiryWorker)"]
        PAY["Payment.Api (:5004)\n+ Payment.Worker (PaymentReconciliationWorker)"]
        SHP["Shipping.Api (:5006)\n+ Shipping.Worker"]
        NTF["Notification.Api (:5008)\n+ Notification.Worker"]
    end

    subgraph Broker["Messaging Tier"]
        RMQ[["RabbitMQ 3.13 (:5672 / UI :15672)\nMassTransit Exchanges & Queues"]]
    end

    subgraph Storage["Data & Cache Tier"]
        REDIS[("Redis 7 (:6379)\nCatalog Read Cache\n& Active Carts")]
        SQL[("SQL Server 2022 (:1433)\nPer-Service Databases & Schemas\n+ Transactional Outbox Tables")]
    end

    subgraph External["External Sandbox Providers"]
        PAY_EXT["Stripe (USD)\nMoMo & VNPay (VND, 1 USD = 25,400 VND)"]
        CAR_EXT["GHN Express (Vietnam)\nShippo (US / International)"]
    end

    SPA -->|"HTTP /api/v1/*"| YARP
    YARP --> ID & CAT & CART & ORD & INV & PAY & SHP & NTF

    CART -.->|"HTTP Price Check"| CAT
    ORD -.->|"HTTP Quote & Clear Cart"| CART

    CAT & CART --> REDIS
    ID & CAT & ORD & INV & PAY & SHP & NTF --> SQL

    ORD <-->|"Saga Commands & Events"| RMQ
    INV <-->|"Reserve / Commit / Release"| RMQ
    PAY <-->|"Charge / Reconcile / Refund"| RMQ
    SHP <-->|"CreateLabel / Handover / Deliver"| RMQ
    NTF <-->|"OrderConfirmed / OrderCancelled"| RMQ

    PAY --> PAY_EXT
    SHP --> CAR_EXT
```

---

## 2. End-to-End Checkout Saga & Transactional Outbox Sequence

Every state transition writes business state and outgoing `MessageEnvelope<T>` records atomically in the same SQL transaction (`InboxState`, `OutboxState`, `OutboxMessage`), relayed asynchronously to RabbitMQ by each service's background worker.

```mermaid
sequenceDiagram
    autonumber
    actor Customer
    participant Web as React SPA (:3000)
    participant GW as YARP Gateway (:5000)
    participant Cart as Cart.Api (:5002)
    participant Order as Order.Api / Order.Worker (:5005)
    participant Inv as Inventory.Worker (:5003)
    participant Pay as Payment.Worker (:5004)
    participant Ship as Shipping.Worker & Api (:5006)
    participant Notif as Notification.Worker (:5008)

    Customer->>Web: Click "Place Order" (Select Payment Method)
    Web->>GW: POST /api/v1/orders/checkout (Idempotency-Key, X-Customer-Id)
    GW->>Order: Forward POST /api/v1/orders/checkout
    Order->>Cart: GET /api/v1/cart/quote (Verify live Catalog prices)
    Cart-->>Order: Verified CheckoutQuoteDto (USD)
    Note over Order: Convert USD -> VND (1:25,400) if MoMo/VNPay<br/>Save Order (Pending) + IdempotencyRecord + Outbox(OrderSubmitted)
    Order->>Cart: DELETE /api/v1/cart (Best-effort clear)
    Order-->>Web: 201 Created (OrderDto: Pending)

    Note over Order: CheckoutStateMachine consumes OrderSubmitted
    Order->>Inv: Publish ReserveInventory (15m TTL) [Order -> Reserving]
    Inv->>Order: Publish InventoryReserved [Order -> Paying]

    Order->>Pay: Publish CollectPayment (Amount, Currency, Method)
    alt Happy Path (Stripe / MoMo / VNPay)
        Pay->>Order: Publish PaymentSucceeded (PaymentAttemptId)
    else Provider Timeout (Timeout = Unknown, NOT Failure)
        Pay->>Order: Publish PaymentPendingResolution [Order -> PaymentUnknown]
        Note over Pay: PaymentReconciliationWorker polls provider every 8s
        Pay->>Order: Publish PaymentSucceeded (or PaymentRejected)
    else Payment Declined
        Pay->>Order: Publish PaymentRejected [Order -> Cancelled]
        Order->>Inv: Publish ReleaseInventory
        Order->>Notif: Publish OrderCancelled
    end

    Order->>Inv: Publish CommitInventory [Saga -> Committing]
    Inv->>Order: Publish InventoryCommitted (Verifies TTL not expired)
    Order->>Ship: Publish CreateShipment [Saga -> Shipping]
    Ship->>Order: Publish ShipmentCreated (ShipmentId, TrackingNumber)
    Order->>Notif: Publish OrderConfirmed [Order -> Confirmed]
    Notif-->>Customer: Dispatch OrderConfirmed Email & SMS

    Note over Web,Ship: Post-Confirmation Physical Carrier Fulfillment (Admin Portal)
    Web->>GW: POST /api/v1/shipments/{id}/handover
    GW->>Ship: RecordCarrierHandover -> Status = HandedOver
    Ship->>Inv: Publish ShipmentHandedOver (Idempotent commit guard)
    Web->>GW: POST /api/v1/shipments/{id}/deliver
    GW->>Ship: RecordShipmentDelivery -> Status = Delivered
```

---

## 3. Order Saga & Carrier Fulfillment State Machines

### 3.1 Order Aggregate & Saga State Machine (`Order.Domain` & `CheckoutStateMachine`)

```mermaid
stateDiagram-v2
    [*] --> Pending: POST /api/v1/orders/checkout
    Pending --> Reserving: OrderSubmitted
    Pending --> Cancelled: Customer Cancel

    Reserving --> Paying: InventoryReserved (15m Hold)
    Reserving --> Cancelled: InventoryRejected / Customer Cancel

    Paying --> PaymentUnknown: PaymentPendingResolution (Timeout)
    Paying --> Confirmed: PaymentSucceeded + InventoryCommitted + ShipmentCreated
    Paying --> Refunding: InventoryCommitFailed / ShipmentFailed
    Paying --> Cancelled: PaymentRejected / InventoryReservationExpired

    PaymentUnknown --> Confirmed: Reconciled PaymentSucceeded + Fulfilled
    PaymentUnknown --> Refunding: PaymentSucceeded after Hold Expired
    PaymentUnknown --> Cancelled: Reconciled PaymentRejected

    Confirmed --> Refunding: Post-Charge Compensation (Pre-Handover Only)

    Refunding --> Cancelled: PaymentRefunded

    Cancelled --> Refunding: Late PaymentSucceeded Arrives (Auto-Compensate)
    Cancelled --> [*]
```

### 3.2 Carrier Shipment State Machine (`Shipping.Domain`)

```mermaid
stateDiagram-v2
    [*] --> Pending: CreateShipment received
    Pending --> LabelCreated: Carrier label assigned (GHN / Shippo / Mock)
    Pending --> ShipmentUnknown: Carrier API timeout (ExternalShipmentRequestId saved)
    Pending --> Failed: Carrier rejected (00000-FAIL -> triggers Order Refunding)
    Pending --> Cancelled: Pre-handover cancellation

    ShipmentUnknown --> LabelCreated: Reconciled carrier label
    ShipmentUnknown --> Cancelled: Pre-handover cancellation

    LabelCreated --> HandedOver: POST /api/v1/shipments/{id}/handover
    LabelCreated --> Cancelled: POST /api/v1/shipments/{id}/cancel (Allowed before handover)

    HandedOver --> InTransit: Carrier scan milestone
    HandedOver --> Delivered: POST /api/v1/shipments/{id}/deliver
    InTransit --> Delivered: POST /api/v1/shipments/{id}/deliver
    Delivered --> [*]
```

> **Physical Handover Guard**: Once a shipment reaches `HandedOver`, `InTransit`, or `Delivered`, `POST /api/v1/shipments/{id}/cancel` and order state transitions out of `Confirmed` are blocked with `409 Conflict` — physical goods require a customer return workflow rather than rolling back a database flag.

---

## 4. Service Boundaries & Database Ownership

Each microservice owns an isolated SQL Server database and schema with its own EF Core migration history and MassTransit Outbox tables (`InboxState`, `OutboxState`, `OutboxMessage`). Cross-database queries are strictly prohibited.

| Service | API Port | Worker Host | SQL Database & Schema | Primary Domain Tables | Cache / External Integration |
|---------|----------|-------------|-----------------------|-----------------------|------------------------------|
| **Gateway** | `:5000` | — | *(Stateless)* | — | YARP reverse proxy, rate limiting, W3C tracing |
| **Identity** | `:5007` | — | `ShopSphereIdentity` (`[identity]`) | `Users`, `Roles`, `Permissions`, `UserRoles`, `RolePermissions`, `RefreshTokens` | JWT Bearer issuance (`HS256`) |
| **Catalog** | `:5001` | `Catalog.Worker` | `ShopSphereCatalog` (`[catalog]`) | `Products`, `PriceHistory` | Redis read-through cache (`ShopSphereCatalog:*`) |
| **Cart** | `:5002` | — | *(Redis Only)* | — | Redis `ShopSphereCart:cart:{customerId}` + HTTP to `Catalog.Api` |
| **Inventory** | `:5003` | `Inventory.Worker` | `ShopSphereInventory` (`[inventory]`) | `StockItems` (`RowVersion`), `Reservations` (15m TTL) | `ReservationExpiryWorker` (sweeps expired holds) |
| **Payment** | `:5004` | `Payment.Worker` | `ShopSpherePayment` (`[payment]`) | `PaymentAttempts` (`RowVersion`), `IdempotencyRecords` | Stripe (`USD`), MoMo & VNPay (`VND`), `PaymentReconciliationWorker` |
| **Order** | `:5005` | `Order.Worker` | `ShopSphereOrders` (`[order]`) | `Orders`, `OrderItems`, `CheckoutSagaStates`, `IdempotencyRecords` | `CheckoutStateMachine`, `OrderStateSyncConsumer`, `USD -> VND` FX |
| **Shipping** | `:5006` | `Shipping.Worker` | `ShopSphere_Shipping` (`[shipping]`) | `Shipments` (`RowVersion`), `ShipmentItems`, `TrackingMilestones` | GHN Sandbox (`VN`), Shippo Sandbox (`US`/Intl), Mock fallback |
| **Notification** | `:5008` | `Notification.Worker` | `ShopSphere_Notification` (`[notification]`) | `NotificationJobs`, `IdempotencyRecords` | Email & SMS dispatchers with non-blocking retry |

---

## 5. Core Architectural Invariants

1. **Transactional Outbox Everywhere**: Business state changes and integration events are committed in the same SQL Server transaction (`AddEntityFrameworkOutbox`). Background workers (`UseBusOutbox`) publish to RabbitMQ after commit.
2. **Idempotency at Every Layer**:
   - **HTTP APIs**: `Idempotency-Key` + SHA-256 request payload fingerprinting (`IdempotencyRecords`); returns `409 Conflict` if the same key is reused with a different payload.
   - **Messaging Consumers**: MassTransit `InboxState` deduplicates by `MessageId`, plus domain guards (`PaymentAttempt`, `Reservation`, `Shipment`, `NotificationJob` unique constraints).
3. **Optimistic Concurrency (`rowversion`)**: Aggregate roots (`Order`, `StockItem`, `Reservation`, `PaymentAttempt`, `Shipment`, `Product`) use SQL Server `rowversion` tokens and handle `DbUpdateConcurrencyException` with bounded retries or explicit `409 Conflict` responses.
4. **Timeout = Unknown, Not Failure**: External payment or carrier timeouts transition to explicit protected states (`PaymentUnknown`, `ShipmentUnknown`) storing the provider operation reference (`PaymentAttemptId`, `ExternalShipmentRequestId`) so reconciliation workers query the provider before retrying or releasing stock.
5. **Late-Event Protection**: A late `PaymentSucceeded` arriving on an already `Cancelled` order never resurrects the order; `CheckoutStateMachine` and `OrderStateSyncConsumer` transition the order to `Refunding` and emit `RefundPayment` to return the customer's funds.

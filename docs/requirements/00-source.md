# Mock Project Details (English translation)

> Translated from the Vietnamese source. The original was flattened (tables and diagrams lost their layout), so tables and diagrams were rebuilt from context. Message names such as `ReserveInventory` are kept as-is.

---

## Shared Technical Foundation

### Service boundaries and communication

Each service owns its business capability, its data, and its rules. Microservices communicate through synchronous APIs (HTTP/gRPC) or an asynchronous messaging system (Command/Event), choosing Command or Event according to the business purpose. A service must not access another microservice's internal data tables directly.

For local development, services may share one SQL Server instance, but they must separate the database or schema, the accounts, and the migrations per service. The data ownership principle is described by Microsoft in *Data sovereignty per microservice*.

- **Synchronous (HTTP/gRPC):** lookups that must return immediately, permission checks, getting a quote, or submitting a request and receiving an acknowledgment code. Avoid long call chains on the user's processing path.
- **Asynchronous (messaging):** long-running business orchestration, updating projections, sending notifications. The API may return a business code and a "processing" status, and the client looks up the result later.
- **Command:** asks a service to perform an action, for example `ReserveInventory`.
- **Event:** announces something that has happened, for example `InventoryReserved`. Multiple services can subscribe to it.

The message names in this document are contracts defined by the project, not built-in APIs or messages of MassTransit.

**Proposed envelope:** `MessageId`, `CorrelationId`, `CausationId`, `OccurredAtUtc`, `ContractVersion`, and a business code. Add `TenantId` only when deploying multiple tenants. Propagate trace context across HTTP, the broker, and workers to link the processing steps together.

### Consistency and error handling

- **Eventual consistency:** services may update at different times, so the API must be able to expose intermediate states.
- **Idempotency:** resending the same operation must not create additional business effects.

| Mechanism | Role and limits |
|---|---|
| **Transactional Outbox** | Writes business data and the pending message in the same local transaction. A relay delivers the message to the broker after commit. It does not create a transaction across services. |
| **MassTransit Bus Outbox** | Suitable when the API/application scope both writes to the DB and sends/publishes. |
| **Consumer Outbox / Inbox pattern** | Coordinates the processing state of incoming messages, business data, and outgoing messages. A business lock against duplicates is still needed for external side effects. |
| **RabbitMQ** | The broker transports messages. Publisher confirms and consumer acknowledgements cover different hops. Do not infer that a business transaction is complete from a broker confirm. |
| **Saga Orchestration** | A coordinator stores state, sends commands, handles results and timeouts. |
| **Saga Choreography** | Participants react to events and perform the next step themselves. There is no central coordinator commanding the whole workflow. |
| **Compensation** | A compensating action, for example returning stock, releasing a slot, or requesting a refund. It is a new business transaction and can fail. |
| **Reconciliation** | Re-checks the result when the provider may have executed the operation but the response was lost. Record the conclusion or hand it off to manual handling. |

**References:**
- Outbox/Inbox: MassTransit Outbox and Outbox Configuration
- Confirm vs. ack: RabbitMQ acknowledgements
- The two Saga approaches and the limits of compensation: Azure Saga pattern

### Proposed implementation rules for workflows

1. Store the idempotency key together with the request fingerprint, the operation code, and the result in the DB. The same key with different content must be rejected.
2. Process messages on the assumption they can be redelivered. Use `MessageId` for deduplication and a business operation ID to prevent duplicated effects.
3. Every resource reservation has an owner, a status, and an expiry. Expiry is a conditional business state transition, not just deleting a cache entry.
4. Protect concurrent updates with constraints and concurrency control. EF Core supports concurrency tokens, and SQL Server `rowversion` is one option, but the application must still handle conflicts (see EF Core concurrency).
5. Retry transient errors with a limit. Business rejections follow the failure branch. Persistently failing messages need an error queue and a controlled replay process.
6. A timeout on an external transaction is an unknown result, not automatically a failure. Store an operation reference so you can inquire before creating a new operation.
7. A late event must not resurrect a cancelled transaction. Check state/version. If a side effect has already happened, open compensation or reconciliation.
8. A failed notification does not reverse completed business. Notifications have their own state and retry.

### Role of the platform stack

| Component | Proposed application |
|---|---|
| **C# / .NET** | Domain model, application handlers, and background workers. Choose a compatible set of versions and lock dependencies at deployment. |
| **ASP.NET Core** | Each service's API, validation, authentication/authorization, and health endpoints. |
| **EF Core / SQL Server** | Persistence, local transactions, migrations, constraints, Saga state, and Outbox, depending on the service. |
| **MassTransit / RabbitMQ** | Consumers, send/publish, correlation, retry/redelivery, and Saga where needed. RabbitMQ handles transport. |
| **Redis** | Cache for read data with TTL, invalidation, and per-service namespaces. It is not by default the source of truth for balances, available slots, or access rights. |
| **OpenTelemetry** | Collects logs, metrics, and distributed traces. Exports to a separately chosen observability backend. |
| **Docker** | Packages services. Docker Compose runs the API, workers, database, broker, and cache locally. |
| **Terraform** | Infrastructure as Code for cloud environments. Manages resources, providers, and state per environment. |

Notes:
- Redis can be integrated through `IDistributedCache` and `Microsoft.Extensions.Caching.StackExchangeRedis` (see Distributed caching).
- OpenTelemetry provides the mechanism for collecting and exporting telemetry. It does not provide a dashboard by itself (see OpenTelemetry in .NET).
- Deployment tool: Terraform.
- OAuth 2.0, Saga, CQRS, Outbox, Inbox, Idempotency, and Docker Compose are protocols or design patterns. Not all of them are packages.
- Topic-specific stacks are analyzed within each project topic.

### MassTransit version

The official documentation now states that MassTransit v9 is a commercial product. When implementing, record the major version, the licensing terms, and the matching documentation. Do not assume that all versions share the same usage terms.

---

## 1. ShopSphere: AI-Powered E-commerce Fulfillment

**Problem:** process orders across catalog, cart, inventory, payment, and shipping. The hard part is making sure the order state correctly reflects the steps that actually happened when only part of the workflow succeeds.

### 1.1. Main Features

| # | Feature / service | Proposed behavior analysis and rules |
|---|---|---|
| 1 | **Catalog**: CatalogService | Manage SKU, name, price, sale status, and price version. Discontinuing a product does not erase its history in old orders. |
| 2 | **Cart**: CartService, CatalogService | Store SKU/quantity. Fetch catalog information to compute the displayed price. At checkout, price and sale status must be re-confirmed. The cart is not an inventory commitment. |
| 3 | **Create order**: OrderService, CartService | Accept an idempotency key. Store a snapshot of the items, unit prices, address, and confirmed total. A resent request returns the same `OrderId`. |
| 4 | **Reserve stock**: InventoryService | Create a reservation by OrderId/SKU, quantity, and expiry. Distinguish available, held, and shipped stock. Prevent two orders from holding the last unit. |
| 5 | **Payment**: PaymentService | Manage payment attempts, the provider's reference, and the unknown state. Webhooks are authenticated and protected against duplicate processing. |
| 6 | **Checkout Saga**: OrderService | Orchestrate reserve stock → payment → shipping, and store state and timeouts. Order state and Saga state have separate purposes but must be reconcilable. |
| 7 | **Shipping**: ShippingService | Create a shipment from an eligible order. Store the tracking code and the handover and delivery status. Prevent creating two shipments for one fulfillment request. |
| 8 | **Cancel order**: OrderService and participants | Check whether the order is already paid/handed over. Release the stock hold, cancel shipping if still allowed, and open a refund request when needed. |
| 9 | **Refund**: PaymentService | Link the refund to the original payment and control the total refunded amount. The order stays in "refund processing" until a confirmed result arrives. |
| 10 | **Tracking**: OrderService, NotificationService | Show progress and business errors. Alert on overdue Sagas and failed compensation. Notification sends results separately and does not decide checkout success or failure. |

### 1.2. Data boundaries

- **CatalogService:** product/SKU/price version.
- **CartService:** cart and cart item.
- **OrderService:** order, price snapshot, cancellation request, and checkout Saga.
- **InventoryService:** stock balance, reservation, and stock movement.
- **PaymentService:** payment attempt, webhook receipt, and refund operation.
- **ShippingService:** shipment and tracking history.
- **NotificationService:** notification job, template, and delivery attempt.

Rules:
- A reservation must check expiry inside the commit operation itself, even if the expiry worker has not run yet.
- The price in an order is a business snapshot. Catalog updates do not change the price of an already accepted order.
- Stock may only be changed at InventoryService. Payment may only be confirmed at PaymentService.

### 1.3. Service interactions

**Main flow (orchestration):**

1. The client reads catalog/cart over HTTP. OrderService takes a snapshot of the cart and confirms the checkout conditions.
2. OrderService commits the order as `Pending`, together with the message that starts the Saga, in the same transaction.
3. The Saga sends `ReserveInventory`. InventoryService replies with `InventoryReserved` or `InventoryRejected`.
4. Once stock is held, the Saga sends `CollectPayment`. PaymentService calls the provider and publishes the verified result.
5. Successful payment triggers `CommitInventory`: Inventory converts the still-valid hold into a committed allocation, so the expiry worker cannot release it. After `InventoryCommitted`, the Saga sends `CreateShipment`. When the shipment is accepted, the order moves to `Confirmed`.
6. When Shipping hands the goods over, it publishes an event so Inventory records the stock-out exactly once. Fulfillment/delivery updates the order lifecycle, and NotificationService sends notifications.

The messages and their order are a proposed contract. Every Saga–participant arrow in the diagrams goes through RabbitMQ.

**Diagram 1: Reserve stock and payment (success path)**

```
Client → Order + Saga : Checkout + idempotency key
Order + Saga → Inventory : ReserveInventory
Inventory → Order + Saga : InventoryReserved
Order + Saga → Payment : CollectPayment
Payment → Order + Saga : PaymentSucceeded
```

**Diagram 2: Commit stock, create shipment, and confirm (after `PaymentSucceeded`)**

```
Order + Saga → Inventory : CommitInventory
Inventory → Order + Saga : InventoryCommitted
Order + Saga → Shipping : CreateShipment
Shipping → Order + Saga : ShipmentCreated
Order + Saga → Notification : OrderConfirmed
```

**Diagram 3: Payment rejected (replaces the success path)**

```
Payment → Order + Saga : PaymentRejected
Order + Saga → Inventory : ReleaseInventory
Order + Saga → Notification : OrderCancelled
```

**Failure paths:**

- **Out of stock:** cancel the checkout before payment. Do not retry a business rejection the way you would a network error.
- **Payment rejected:** release the reservation.
- **Payment timeout:** keep the state `PaymentUnknown` and inquire using the same reference. If the hold has expired but the payment actually succeeded, open a refund/reconciliation.
- **Shipping failure after payment:** transient errors continue to be processed with a limit. Unrecoverable errors lead to compensation according to policy. Once goods are physically handed over, a returns process is needed, not just rolling back a flag in the DB.

### 1.4. Technical stack details

| Stack in this topic | Specific application |
|---|---|
| **.NET / ASP.NET Core** | Catalog/cart/order APIs and the payment, inventory, and shipping workers. Validation before accepting a checkout. |
| **EF Core / SQL Server** | Order snapshot, reservation, payment/refund, and Saga persistence. Unique keys for checkout/payment operations, and concurrency control for stock. |
| **MassTransit / RabbitMQ** | Commands to participants and result events. An independent queue for notification so that a failed send does not block checkout. |
| **Transactional Outbox** | Write order/event, reservation/event, and payment result/event in the owning service's transaction. |
| **Saga Orchestration** | The Checkout Saga belongs to OrderService. It stores OrderId, reservation/payment/shipment references, and deadlines. |
| **Redis** | Cache the catalog and the cart read model, with invalidation when prices change. Do not use a stock cache to approve holds. |
| **Polly** | Resilience for HTTP calls to payment/shipping providers. In .NET you can use `Microsoft.Extensions.Http.Resilience`, which is built on Polly. Do not blindly retry charge operations. |
| **OpenTelemetry** | Traces from checkout through each participant. Metrics for checkout duration, outbox backlog, and overdue Sagas. |
| **Docker / Terraform** | Compose for local. Terraform declares the infrastructure of the target environment, with configuration and secrets separated per service. |

Basis for resilience: HTTP resilience in .NET.

**AI Stretch:** Semantic Kernel connects an assistant to catalog lookup and RAG over the FAQ and return policy. Prices, stock, and checkout operations come from permission-controlled business APIs. AI answers do not create stock commitments. For the AI platform, see the shared reference section.
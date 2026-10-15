# ShopSphere Services

## Problem Statement

Process orders across catalog, cart, inventory, payment, and shipping. The hard part is making sure the order state correctly reflects the steps that actually happened when only part of the workflow succeeds.

## Main Features

| # | Feature / Service | Proposed Behavior Analysis and Rules |
|---|-------------------|-------------------------------------|
| 1 | **Catalog: CatalogService** | Manage SKU, name, price, sale status, and price version. Discontinuing a product does not erase its history in old orders. |
| 2 | **Cart: CartService, CatalogService** | Store SKU/quantity. Fetch catalog information to compute the displayed price. At checkout, price and sale status must be re-confirmed. The cart is not an inventory commitment. |
| 3 | **Create order: OrderService, CartService** | Accept an idempotency key. Store a snapshot of the items, unit prices, address, and confirmed total. A resent request returns the same OrderId. |
| 4 | **Reserve stock: InventoryService** | Create a reservation by OrderId/SKU, quantity, and expiry. Distinguish available, held, and shipped stock. Prevent two orders from holding the last unit. |
| 5 | **Payment: PaymentService** | Manage payment attempts, the provider's reference, and the unknown state. Webhooks are authenticated and protected against duplicate processing. |
| 6 | **Checkout Saga: OrderService** | Orchestrate reserve stock → payment → shipping, and store state and timeouts. Order state and Saga state have separate purposes but must be reconcilable. |
| 7 | **Shipping: ShippingService** | Create a shipment from an eligible order. Store the tracking code and the handover and delivery status. Prevent creating two shipments for one fulfillment request. |
| 8 | **Cancel order: OrderService and participants** | Check whether the order is already paid/handed over. Release the stock hold, cancel shipping if still allowed, and open a refund request when needed. |
| 9 | **Refund: PaymentService** | Link the refund to the original payment and control the total refunded amount. The order stays in "refund processing" until a confirmed result arrives. |
| 10 | **Tracking: OrderService, NotificationService** | Show progress and business errors. Alert on overdue Sagas and failed compensation. Notification sends results separately and does not decide checkout success or failure. |

## Data Boundaries

- **CatalogService**: product/SKU/price version
- **CartService**: cart and cart item
- **OrderService**: order, price snapshot, cancellation request, and checkout Saga
- **InventoryService**: stock balance, reservation, and stock movement
- **PaymentService**: payment attempt, webhook receipt, and refund operation
- **ShippingService**: shipment and tracking history
- **NotificationService**: notification job, template, and delivery attempt

### Rules

1. A reservation must check expiry inside the commit operation itself, even if the expiry worker has not run yet.
2. The price in an order is a business snapshot. Catalog updates do not change the price of an already accepted order.
3. Stock may only be changed at InventoryService. Payment may only be confirmed at PaymentService.

## Service Interactions

### Main Flow (Orchestration)

1. The client reads catalog/cart over HTTP. OrderService takes a snapshot of the cart and confirms the checkout conditions.

2. OrderService commits the order as Pending, together with the message that starts the Saga, in the same transaction.

3. The Saga sends `ReserveInventory`. InventoryService replies with `InventoryReserved` or `InventoryRejected`.

4. Once stock is held, the Saga sends `CollectPayment`. PaymentService calls the provider and publishes the verified result.

5. Successful payment triggers `CommitInventory`: Inventory converts the still-valid hold into a committed allocation, so the expiry worker cannot release it. After `InventoryCommitted`, the Saga sends `CreateShipment`. When the shipment is accepted, the order moves to Confirmed.

6. When Shipping hands the goods over, it publishes an event so Inventory records the stock-out exactly once. Fulfillment/delivery updates the order lifecycle, and NotificationService sends notifications.

> **Note**: The messages and their order are a proposed contract. Every Saga–participant arrow in the diagrams goes through RabbitMQ.

### Success Path Diagrams

**Diagram 1: Reserve stock and payment (success path)**

```
Client → Order + Saga : Checkout + idempotency key
Order + Saga → Inventory : ReserveInventory
Inventory → Order + Saga : InventoryReserved
Order + Saga → Payment : CollectPayment
Payment → Order + Saga : PaymentSucceeded
```

**Diagram 2: Commit stock, create shipment, and confirm (after PaymentSucceeded)**

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

### Failure Paths

- **Out of stock**: Cancel the checkout before payment. Do not retry a business rejection the way you would a network error.

- **Payment rejected**: Release the reservation.

- **Payment timeout**: Keep the state `PaymentUnknown` and inquire using the same reference. If the hold has expired but the payment actually succeeded, open a refund/reconciliation.

- **Shipping failure after payment**: Transient errors continue to be processed with a limit. Unrecoverable errors lead to compensation according to policy. Once goods are physically handed over, a returns process is needed, not just rolling back a flag in the DB.

## Technical Stack Details

| Stack Component | Specific Application |
|----------------|---------------------|
| **.NET / ASP.NET Core** | Catalog/cart/order APIs and the payment, inventory, and shipping workers. Validation before accepting a checkout. |
| **EF Core / SQL Server** | Order snapshot, reservation, payment/refund, and Saga persistence. Unique keys for checkout/payment operations, and concurrency control for stock. |
| **MassTransit / RabbitMQ** | Commands to participants and result events. An independent queue for notification so that a failed send does not block checkout. |
| **Transactional Outbox** | Write order/event, reservation/event, and payment result/event in the owning service's transaction. |
| **Saga Orchestration** | The Checkout Saga belongs to OrderService. It stores OrderId, reservation/payment/shipment references, and deadlines. |
| **Redis** | Cache the catalog and the cart read model, with invalidation when prices change. Do not use a stock cache to approve holds. |
| **Polly** | Resilience for HTTP calls to payment/shipping providers. In .NET you can use `Microsoft.Extensions.Http.Resilience`, which is built on Polly. Do not blindly retry charge operations. |
| **OpenTelemetry** | Traces from checkout through each participant. Metrics for checkout duration, outbox backlog, and overdue Sagas. |
| **Docker / Terraform** | Compose for local. Terraform declares the infrastructure of the target environment, with configuration and secrets separated per service. |

**Basis for resilience**: [HTTP resilience in .NET](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience)

## AI Stretch

Semantic Kernel connects an assistant to catalog lookup and RAG over the FAQ and return policy. Prices, stock, and checkout operations come from permission-controlled business APIs. AI answers do not create stock commitments. For the AI platform, see the shared reference section.

# Order Service & Checkout State Machine Saga Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the Order Service (`Order.Domain`, `Order.Application`, `Order.Infrastructure`, `Order.Api`, `Order.Worker`, `Order.Tests.Unit`) including the Order aggregate root with immutable price snapshots, EF Core persistence with SQL idempotency store and MassTransit Outbox, the MassTransit `CheckoutStateMachine` Saga (happy path, failure compensations, timeout `PaymentUnknown` handling, and late-event compensation), and the HTTP API with OpenAPI specification.

**Architecture:** Clean Architecture (`Domain` -> `Application` -> `Infrastructure` -> `Api`/`Worker`) backed by SQL Server (`ShopSphereOrders`, schema `order`) with optimistic concurrency (`rowversion`) and MassTransit Entity Framework Outbox. Cross-service message contracts use exact matching namespaces (`ShopSphere.Services.Inventory.Application.Contracts`, `ShopSphere.Services.Payment.Application.Contracts`, `ShopSphere.Services.Shipping.Application.Contracts`, `ShopSphere.Services.Order.Application.Contracts`) so MassTransit RabbitMQ exchanges route seamlessly without coupling service projects.

**Tech Stack:** .NET 10, Entity Framework Core 10 (`Microsoft.EntityFrameworkCore.SqlServer`), MassTransit 8.3.3 (`MassTransit`, `MassTransit.EntityFrameworkCore`, `MassTransit.RabbitMQ`), FluentValidation, Swashbuckle OpenAPI 3.0, xUnit, FluentAssertions, Moq.

---

### Task 1: Order Domain Model & Price Snapshot ([Task-38])

**Files:**
- Create: `src/Services/Order/Order.Domain/Order.Domain.csproj`
- Create: `src/Services/Order/Order.Domain/OrderStatus.cs`
- Create: `src/Services/Order/Order.Domain/ShippingAddress.cs`
- Create: `src/Services/Order/Order.Domain/OrderItem.cs`
- Create: `src/Services/Order/Order.Domain/Order.cs`
- Create: `src/Services/Order/Order.Domain/Events/OrderDomainEvents.cs`
- Create: `src/Services/Order/Order.Domain/Exceptions/OrderDomainExceptions.cs`
- Create: `tests/Order.Tests.Unit/Order.Tests.Unit.csproj`
- Create: `tests/Order.Tests.Unit/Domain/OrderTests.cs`

- [x] **Step 1: Scaffold `Order.Domain` and `Order.Tests.Unit` projects and add to `ShopSphere.sln`**
- [x] **Step 2: Write failing unit tests in `tests/Order.Tests.Unit/Domain/OrderTests.cs`** covering:
  - Order creation with valid items, shipping address, payment method, and automatic `TotalAmount` calculation from `OrderItem` snapshots.
  - Validation failures (empty customerId, empty items, negative price, non-positive quantity, invalid shipping address).
  - State transitions across all 7 `OrderStatus` values (`Pending`, `Reserving`, `Paying`, `PaymentUnknown`, `Confirmed`, `Refunding`, `Cancelled`).
  - Late event / invalid transition guards (`MarkReserving` on `Cancelled` throws, `MarkConfirmed` on `Cancelled` throws, `MarkCancelled` on `Confirmed` throws, `MarkRefunding` allowed from `Paying`, `PaymentUnknown`, `Confirmed`, and `Cancelled` when late payment arrives).
- [x] **Step 3: Implement `OrderStatus`, `ShippingAddress`, `OrderItem`, `Order`, `OrderDomainEvents`, and `OrderDomainExceptions`**
- [x] **Step 4: Run `dotnet test tests/Order.Tests.Unit/Order.Tests.Unit.csproj` and verify all domain tests pass**

---

### Task 2: EF Core Persistence, SQL Idempotency Store, Cart HTTP Client & Migrations ([Task-39])

**Files:**
- Create: `src/Services/Order/Order.Application/Order.Application.csproj`
- Create: `src/Services/Order/Order.Application/Common/Interfaces/IOrderDbContext.cs`
- Create: `src/Services/Order/Order.Application/Common/Interfaces/ICartClient.cs`
- Create: `src/Services/Order/Order.Application/Saga/CheckoutSagaState.cs`
- Create: `src/Services/Order/Order.Infrastructure/Order.Infrastructure.csproj`
- Create: `src/Services/Order/Order.Infrastructure/OrderDbContext.cs`
- Create: `src/Services/Order/Order.Infrastructure/OrderDbContextFactory.cs`
- Create: `src/Services/Order/Order.Infrastructure/Idempotency/EfIdempotencyStore.cs`
- Create: `src/Services/Order/Order.Infrastructure/Clients/CartHttpClient.cs`
- Create: `src/Services/Order/Order.Infrastructure/DependencyInjection.cs`
- Create: `src/Services/Order/Order.Infrastructure/Migrations/*_InitialOrderMigration.cs`
- Create: `tests/Order.Tests.Unit/Helpers/MockDbSetExtensions.cs`
- Create: `tests/Order.Tests.Unit/Infrastructure/OrderDbContextTests.cs`
- Create: `tests/Order.Tests.Unit/Infrastructure/EfIdempotencyStoreTests.cs`
- Create: `tests/Order.Tests.Unit/Infrastructure/CartHttpClientTests.cs`

- [x] **Step 1: Write failing unit tests for `OrderDbContext`, `EfIdempotencyStore`, and `CartHttpClient`**
- [x] **Step 2: Implement `OrderDbContext` (`order` schema, `Orders`, `OrderItems`, `CheckoutSagaStates`, `IdempotencyRecords`, `OutboxMessages`, and `AddShopSphereOutboxEntities()`), `EfIdempotencyStore`, and `CartHttpClient`**
- [x] **Step 3: Generate EF Core migration `InitialOrderMigration` and apply to local `ShopSphereOrders` database**
- [x] **Step 4: Run `dotnet test tests/Order.Tests.Unit/Order.Tests.Unit.csproj` and verify all infrastructure tests pass**

---

### Task 3: MassTransit Checkout State Machine Saga — Happy Path, Compensations, Timeouts & Late Events ([Task-42], [Task-43], [Task-44], [Task-45])

**Files:**
- Create: `src/Services/Order/Order.Application/Contracts/OrderContracts.cs`
- Create: `src/Services/Order/Order.Application/Saga/CheckoutStateMachine.cs`
- Create: `src/Services/Order/Order.Application/Saga/OrderStateSyncConsumer.cs`
- Create: `src/Services/Order/Order.Application/DependencyInjection.cs`
- Create: `src/Services/Order/Order.Worker/Order.Worker.csproj`
- Create: `src/Services/Order/Order.Worker/Program.cs`
- Create: `src/Services/Order/Order.Worker/appsettings.json`
- Create: `tests/Order.Tests.Unit/Saga/CheckoutStateMachineTests.cs`

- [x] **Step 1: Write failing Saga unit tests in `tests/Order.Tests.Unit/Saga/CheckoutStateMachineTests.cs`** using MassTransit's `InMemoryTestHarness` covering:
  - Happy path (`OrderSubmitted` -> `Reserving` (`ReserveInventory`) -> `InventoryReserved` -> `Paying` (`CollectPayment`) -> `PaymentSucceeded` -> `Committing` (`CommitInventory`) -> `InventoryCommitted` -> `Shipping` (`CreateShipment`) -> `ShipmentCreated` -> `Confirmed` (`OrderConfirmed`)).
  - Out of stock (`InventoryRejected` in `Reserving` -> `Cancelled` + `OrderCancelled`).
  - Payment rejected (`PaymentRejected` in `Paying` -> `Cancelled` + `ReleaseInventory` + `OrderCancelled`).
  - Payment timeout (`PaymentPendingResolution` in `Paying` -> `PaymentUnknown`, order NOT failed or double-charged; subsequent `PaymentSucceeded` -> `Committing`; or subsequent `PaymentRejected` -> `Cancelled` + `ReleaseInventory`).
  - Reservation expiry during `Paying` (`InventoryReservationExpired` -> `Cancelled` + `OrderCancelled`).
  - Reservation expiry during `PaymentUnknown` (`InventoryReservationExpired` -> `IsReservationExpired = true`; if `PaymentSucceeded` arrives afterward -> transitions to `Refunding` and publishes `RefundPayment`!).
  - Commit failure (`InventoryCommitFailed` in `Committing` -> `Refunding` + `RefundPayment` -> `PaymentRefunded` -> `Cancelled` + `OrderCancelled`).
  - Shipment failure (`ShipmentFailed` in `Shipping` -> `Refunding` + `RefundPayment` -> `PaymentRefunded` -> `Cancelled` + `OrderCancelled`).
  - **Late Event Protection**: Late `PaymentSucceeded` arriving when Saga is already `Cancelled` -> does NOT resurrect order; transitions to `Refunding` and emits `RefundPayment`. Late `InventoryReserved` arriving when Saga is `Cancelled` -> stays `Cancelled` and emits `ReleaseInventory`.
- [x] **Step 2: Implement `OrderContracts.cs`, `CheckoutStateMachine.cs`, `OrderStateSyncConsumer.cs`, and `Order.Worker`**
- [x] **Step 3: Run `dotnet test tests/Order.Tests.Unit/Order.Tests.Unit.csproj` and verify all Saga tests pass**

---

### Task 4: Idempotent Checkout HTTP API, Order Queries & OpenAPI Specification ([Task-40])

**Files:**
- Create: `src/Services/Order/Order.Application/Orders/Dtos/OrderDtos.cs`
- Create: `src/Services/Order/Order.Application/Orders/Commands/SubmitCheckout/SubmitCheckoutCommand.cs`
- Create: `src/Services/Order/Order.Application/Orders/Commands/CancelOrder/CancelOrderCommand.cs`
- Create: `src/Services/Order/Order.Application/Orders/Queries/GetOrderById/GetOrderByIdQuery.cs`
- Create: `src/Services/Order/Order.Application/Orders/Queries/GetOrders/GetOrdersQuery.cs`
- Create: `src/Services/Order/Order.Api/Order.Api.csproj`
- Create: `src/Services/Order/Order.Api/OrderApiEndpoints.cs`
- Create: `src/Services/Order/Order.Api/Program.cs`
- Create: `src/Services/Order/Order.Api/appsettings.json`
- Create: `src/Services/Order/Order.Api/Health/SqlServerHealthCheck.cs`
- Create: `src/Services/Order/Order.Api/Health/RabbitMqHealthCheck.cs`
- Create: `src/Services/Order/Order.Api/openapi.json`
- Create: `docs/specs/order-openapi.json`
- Create: `tests/Order.Tests.Unit/Api/OrderApiEndpointTests.cs`
- Create: `tests/Order.Tests.Unit/OpenApiSpecGeneratorTests.cs`

- [x] **Step 1: Write failing unit tests for Checkout command handler, Idempotency-Key enforcement, Order endpoints, and OpenAPI spec generation**
- [x] **Step 2: Implement `SubmitCheckoutCommandHandler` (verifying live cart quote via `ICartClient`, creating `Order` aggregate with price snapshot, publishing `OrderSubmitted`), `CancelOrderCommandHandler`, `GetOrderByIdQueryHandler`, `GetOrdersQueryHandler`, and `OrderApiEndpoints` (enforcing required `Idempotency-Key` header + `EfIdempotencyStore` fingerprint verification)**
- [x] **Step 3: Run `OpenApiSpecGeneratorTests` to generate `src/Services/Order/Order.Api/openapi.json` and `docs/specs/order-openapi.json`**
- [x] **Step 4: Run full lint, doctor, and unit test suite; commit with Conventional Commits referencing Notion task IDs**

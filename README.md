# ShopSphere

AI-powered e-commerce fulfillment platform built with .NET microservices. Orchestrates catalog, cart, inventory, payment, shipping, identity, and notifications through resilient workflows with saga orchestration and transactional messaging.

## Table of Contents

- [Quick Start (5 Minutes)](#quick-start-5-minutes)
  - [Prerequisites](#prerequisites)
  - [Setup](#setup)
- [Running & Monitoring All Services Locally](#running--monitoring-all-services-locally)
  - [Option A: Single-Command Startup (Recommended)](#option-a-single-command-startup-recommended)
  - [Option B: Manual Step-by-Step Startup](#option-b-manual-step-by-step-startup)
  - [Service Ports & Health Check URLs](#service-ports--health-check-urls)
  - [Monitoring & Observability Endpoints](#monitoring--observability-endpoints)
  - [Credentials Reference](#credentials-reference)
- [Order Saga Lifecycle & Payment Flows](#order-saga-lifecycle--payment-flows)
  - [End-to-End Saga State Machine](#end-to-end-saga-state-machine)
  - [Hardcoded USD to VND Currency Exchange](#hardcoded-usd-to-vnd-currency-exchange)
  - [How Each Payment Mode Affects the Lifecycle](#how-each-payment-mode-affects-the-lifecycle)
  - [Monitoring & Validation Guide for Each Step & Payment Scenario](#monitoring--validation-guide-for-each-step--payment-scenario)
    - [1. Step-by-Step Lifecycle Validation Checklist](#1-step-by-step-lifecycle-validation-checklist)
    - [2. Validating Each Payment Scenario](#2-validating-each-payment-scenario)
    - [3. Quick SQL & Redis Inspection Commands](#3-quick-sql--redis-inspection-commands)
- [One-Time Manual Steps](#one-time-manual-steps)
  - [GitHub Authentication](#github-authentication)
  - [Notion Integration](#notion-integration)
- [Token Creation](#token-creation)
  - [GitHub Fine-Grained Personal Access Token](#github-fine-grained-personal-access-token)
  - [Notion Integration Token](#notion-integration-token)
- [Architecture](#architecture)
- [Project Structure](#project-structure)
- [Technology Stack](#technology-stack)
- [Development Workflow](#development-workflow)
- [Agent Support](#agent-support)
- [MCP Servers](#mcp-servers)
- [Useful Commands](#useful-commands)
- [Troubleshooting](#troubleshooting)
- [Contributing](#contributing)
- [Resources](#resources)
- [License](#license)
- [Support](#support)

---

## Quick Start (5 Minutes)

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop) running
- [Visual Studio Code](https://code.visualstudio.com/) with [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)
- [Git](https://git-scm.com/)

### Setup

1. **Clone the repository**:
   ```bash
   git clone <repository-url>
   cd ShopSphere
   ```

2. **Create your environment file**:
   ```bash
   cp .env.example .env
   ```

3. **Add your tokens** (see [Token Creation](#token-creation) below):
   ```bash
   # Edit .env
   GITHUB_TOKEN=ghp_YOUR_TOKEN_HERE
   NOTION_TOKEN=secret_YOUR_TOKEN_HERE
   ```

4. **Open in Dev Container**:
   - Open folder in VS Code
   - When prompted, click "Reopen in Container"
   - Or: `Ctrl+Shift+P` → "Dev Containers: Reopen in Container"
   - Wait for build + bootstrap (2-3 minutes first time)

5. **Verify environment**:
   ```bash
   bash scripts/doctor.sh
   ```
   Should show all ✅ checks passing.

6. **Start infrastructure**:
   ```bash
   docker compose -f deploy/compose/docker-compose.yml up -d
   ```
   Wait ~30 seconds for health checks.

7. **Verify infrastructure**:
   ```bash
   docker compose -f deploy/compose/docker-compose.yml ps
   ```
   All services should show "healthy" status.

**You're ready!** All microservices are implemented: Identity (`:5007`), Catalog (`:5001`), Cart (`:5002`), Inventory (`:5003`), Payment (`:5004`), Order (`:5005`), Shipping (`:5006`), Notification (`:5008`), YARP API Gateway (`:5000`), and the React Storefront & Admin Operations Portal (`:3000`).

---

## Running & Monitoring All Services Locally

### Option A: Single-Command Startup (Recommended)

You can start Docker infrastructure (SQL Server, RabbitMQ, Redis), build the solution once, launch all 8 microservice APIs, 6 background workers, the YARP API Gateway, and the Vite React Frontend with a single script:

```powershell
# PowerShell (Windows / Cross-platform)
pwsh scripts/start-dev.ps1

# Or open each service in its own separate PowerShell window:
pwsh scripts/start-dev.ps1 -SeparateWindows
```

```bash
# Bash (Linux / macOS / Git Bash / WSL)
bash scripts/start-dev.sh
```

Press `Ctrl+C` at any time to gracefully stop all background service processes.

### Option B: Manual Step-by-Step Startup

If you prefer to run specific services individually for debugging:

```powershell
# 1. Start Docker infrastructure
docker compose -f deploy/compose/docker-compose.yml up -d

# 2. Run APIs (in separate terminals)
dotnet run --project src/Services/Catalog/Catalog.Api --urls http://localhost:5001
dotnet run --project src/Services/Cart/Cart.Api --urls http://localhost:5002
dotnet run --project src/Services/Inventory/Inventory.Api --urls http://localhost:5003
dotnet run --project src/Services/Payment/Payment.Api --urls http://localhost:5004
dotnet run --project src/Services/Order/Order.Api --urls http://localhost:5005
dotnet run --project src/Services/Shipping/Shipping.Api --urls http://localhost:5006
dotnet run --project src/Services/Identity/Identity.Api --urls http://localhost:5007
dotnet run --project src/Services/Notification/Notification.Api --urls http://localhost:5008

# 3. Run Background Workers (Outbox Relays & Reconciliation)
dotnet run --project src/Services/Catalog/Catalog.Worker
dotnet run --project src/Services/Inventory/Inventory.Worker
dotnet run --project src/Services/Payment/Payment.Worker
dotnet run --project src/Services/Order/Order.Worker
dotnet run --project src/Services/Shipping/Shipping.Worker
dotnet run --project src/Services/Notification/Notification.Worker

# 4. Run YARP API Gateway
dotnet run --project src/Gateway/ShopSphere.Gateway --urls http://localhost:5000

# 5. Run React Frontend SPA
pnpm --dir src/Web run dev
```

### Service Ports & Health Check URLs

All SQL databases are automatically migrated and seeded with default products (`CatalogDbSeeder`), stock (`InventoryDbSeeder`), and roles/admin account (`IdentityDbSeeder`) when started in `Development` mode.

| Component | Local URL | Health Check | Swagger / OpenAPI UI |
|-----------|-----------|--------------|----------------------|
| **React Storefront & Admin Portal** | `http://localhost:3000` | — | — |
| **YARP API Gateway (BFF)** | `http://localhost:5000` | `http://localhost:5000/health` | `http://localhost:5000/openapi.json` |
| **Catalog.Api** | `http://localhost:5001` | `http://localhost:5001/health` | `http://localhost:5001/swagger` |
| **Cart.Api** | `http://localhost:5002` | `http://localhost:5002/health` | `http://localhost:5002/swagger` |
| **Inventory.Api** | `http://localhost:5003` | `http://localhost:5003/health` | `http://localhost:5003/swagger` |
| **Payment.Api** | `http://localhost:5004` | `http://localhost:5004/health` | `http://localhost:5004/swagger` |
| **Order.Api** | `http://localhost:5005` | `http://localhost:5005/health` | `http://localhost:5005/swagger` |
| **Shipping.Api** | `http://localhost:5006` | `http://localhost:5006/health` | `http://localhost:5006/swagger` |
| **Identity.Api** | `http://localhost:5007` | `http://localhost:5007/health` | `http://localhost:5007/swagger` |
| **Notification.Api** | `http://localhost:5008` | `http://localhost:5008/health` | `http://localhost:5008/swagger` |

### Monitoring & Observability Endpoints

1. **Quick Health Check (PowerShell)**:
   ```powershell
   5000..5008 | ForEach-Object {
     $res = Invoke-RestMethod "http://localhost:$_/health" -ErrorAction SilentlyContinue
     Write-Host "Port $_ : $($res.status)"
   }
   ```
2. **RabbitMQ Management Dashboard**:
   - Open `http://localhost:15672` (Login: `guest` / `guest`).
   - Inspect **Queues and Streams** (`reserve-inventory`, `collect-payment`, `commit-inventory`, `create-shipment`, `refund-payment`, `order-state-sync`, etc.) to monitor live message rates and dead-letter (`_error`) queues.
3. **Redis Cart Inspection (Docker Desktop / CLI)**:
   ```bash
   docker exec -it shopsphere-redis redis-cli
   # Inside redis-cli:
   KEYS *
   GET "ShopSphere_Cart:cart:cust-alex-01"
   ```
4. **Docker Container Resource & Log Monitoring**:
   ```bash
   docker compose -f deploy/compose/docker-compose.yml ps
   docker compose -f deploy/compose/docker-compose.yml logs -f sqlserver rabbitmq redis
   ```

### Credentials Reference

All local development credentials (SQL Server `sa` password, per-service database names, RabbitMQ login, default Admin user, sandbox payment keys, webhook HMAC secret, and JWT signing key) are documented in [**docs/CREDENTIALS.md**](./docs/CREDENTIALS.md).

---

## Order Saga Lifecycle & Payment Flows

ShopSphere uses a **MassTransit State Machine Saga (`CheckoutStateMachine`)** paired with **Transactional Outbox** tables (`InboxState`, `OutboxState`, `OutboxMessage`) and `OrderStateSyncConsumer` to coordinate distributed checkout across **Order**, **Inventory**, **Payment**, **Shipping**, and **Notification** services without distributed two-phase locks.

### End-to-End Saga State Machine

| Step | Saga / Order State | Trigger | Command / Event Dispatched | What Happens Under the Hood |
|------|--------------------|---------|----------------------------|-----------------------------|
| **1** | `Pending` | `POST /api/v1/orders/checkout` | `OrderSubmitted` | `Order.Api` fetches a verified price quote from `Cart.Api`, snapshots items (converting `USD` → `VND` if MoMo/VNPay is selected), saves `Order` (`Pending`) + `OrderSubmitted` outbox event, and clears the Redis cart. |
| **2** | `Reserving` | `OrderSubmitted` | `ReserveInventory` | `CheckoutStateMachine` transitions to `Reserving` and publishes `ReserveInventory`. `OrderStateSyncConsumer` updates the `Order` aggregate to `Reserving`. |
| **3** | `Paying` | `InventoryReserved` | `CollectPayment` | `ReserveInventoryConsumer` locks stock with a 15-minute TTL (`StockReservation` in `Held` status) and publishes `InventoryReserved`. Saga transitions to `Paying` and emits `CollectPayment`. |
| **4a** | `PaymentUnknown` *(if timeout)* | `PaymentPendingResolution` | *(Reconciliation Sweep)* | **Timeout = Unknown, NOT Failure**: If the payment gateway times out or drops connection, `CollectPaymentConsumer` marks the attempt `PaymentUnknown` and emits `PaymentPendingResolution`. Inventory remains held while `PaymentReconciliationWorker` queries the provider every 8s. |
| **4b** | `Committing` | `PaymentSucceeded` | `CommitInventory` | Once payment succeeds (immediately or via reconciliation), Saga transitions to `Committing` and emits `CommitInventory`. |
| **5** | `Shipping` | `InventoryCommitted` | `CreateShipment` | `CommitInventoryConsumer` verifies the hold has not expired, permanently deducts stock, and emits `InventoryCommitted`. Saga transitions to `Shipping` and emits `CreateShipment`. |
| **6** | `Confirmed` | `ShipmentCreated` | `OrderConfirmed` | `CreateShipmentConsumer` routes to **Shippo** (US/Intl), **GHN** (Vietnam `VN`), or **MockCarrierProviderClient**, assigns a tracking number, and emits `ShipmentCreated`. Saga transitions to `Confirmed` and emits `OrderConfirmed` (triggering Email/SMS in `NotificationService`). |
| **7** | `Refunding` → `Cancelled` *(Compensation)* | `InventoryCommitFailed` / `ShipmentFailed` / Late `PaymentSucceeded` | `RefundPayment` → `PaymentRefunded` → `OrderCancelled` | If inventory commit or shipment creation fails after payment was captured (or a late `PaymentSucceeded` arrives on an already-cancelled order), the Saga transitions to `Refunding`, refunds the charge, and settles in `Cancelled`. |

### Hardcoded USD to VND Currency Exchange

Catalog products are priced in `USD`. Because Vietnamese payment gateways (**MoMo** and **VNPay**) process transactions in `VND` and validate total transaction amounts in `VND`, ShopSphere applies a deterministic hardcoded exchange rate (**`1 USD = 25,400 VND`**):

- **Frontend (`CheckoutWizard`, `CustomerOrderReceipt`, `LiveOrderTracker`, `AdminPortal`)**: Selecting **MoMo E-Wallet** (`digital_wallet`) or **VNPay Banking / QR** (`bank_transfer`) displays the `FX: 1 USD = 25,400 VND` badge and shows converted `VND` item totals and `Total Due` alongside the `USD` equivalent.
- **Order Service (`SubmitCheckoutCommandHandler`)**: Automatically converts snapshot item prices and `Order.TotalAmount` from `USD` to `VND` (`Currency = "VND"`) when a VND payment method is selected.
- **Payment Service (`MoMoPaymentProviderClient` & `VnPayPaymentProviderClient`)**: Converts any non-VND amount using `1 USD = 25,400 VND` for both charges and refunds, and clamps MoMo Sandbox amounts to MoMo's required test bounds (`1,000 VND` – `50,000,000 VND`).

### How Each Payment Mode Affects the Lifecycle

In the Checkout modal (`PaymentSelectionForm`), you can select any of the **3 Sandbox Providers** or **3 Simulated Failure/Timeout Test Modes**, routed by `RoutingPaymentProviderClient`:

#### 1. `credit_card` — Stripe Sandbox / Mock Card (USD Happy Path)
- **Flow**: `Pending` → `Reserving` → `Paying` → `Committing` → `Shipping` → **`Confirmed`**
- **What happens**:
  - Uses `StripePaymentProviderClient` if `PaymentSettings:Stripe:ApiKey` is configured, or falls back automatically to `MockPaymentProviderClient` (`FallbackToMockOnMissingCredentials = true`).
  - Immediately approves the charge (`mock_tx_<guid>` or `pi_<id>`) and publishes `PaymentSucceeded`.
  - Inventory hold is committed, carrier label is created, and order finishes at **`Confirmed`**.

#### 2. `digital_wallet` — MoMo E-Wallet Sandbox (`VND`)
- **Flow**: `Pending` → `Reserving` → `Paying` → `Committing` → `Shipping` → **`Confirmed`** (or `PaymentUnknown` if offline)
- **What happens**:
  - `SubmitCheckoutCommand` converts the order snapshot from `USD` to `VND` (`1 USD = 25,400 VND`).
  - `RoutingPaymentProviderClient` routes to `MoMoPaymentProviderClient`, which signs an HMAC-SHA256 request to `https://test-payment.momo.vn/v2/gateway/api/create`.

#### 3. `bank_transfer` — VNPay Banking / QR Sandbox (`VND`)
- **Flow**: `Pending` → `Reserving` → `Paying` → `Committing` → `Shipping` → **`Confirmed`**
- **What happens**:
  - `SubmitCheckoutCommand` converts the order snapshot from `USD` to `VND` (`1 USD = 25,400 VND`).
  - `RoutingPaymentProviderClient` routes to `VnPayPaymentProviderClient`, which generates a signed VNPay 2.1.0 transaction (`vnp_<attemptId>`) and publishes `PaymentSucceeded`.

#### 4. `decline` — `[Test] Declined Card (Insufficient Funds)`
- **Flow**: `Pending` → `Reserving` → `Paying` → **`Cancelled`**
- **What happens**:
  - `MockPaymentProviderClient` immediately returns `PaymentProviderStatus.Failed` (`"Payment declined by provider: insufficient funds."`).
  - `CollectPaymentConsumer` marks `PaymentAttempt` as `Failed` (`4`) and publishes `PaymentRejected`.
  - `CheckoutStateMachine` transitions the order to **`Cancelled`**, publishes `ReleaseInventory` (returning held stock to `QuantityAvailable`), and emits `OrderCancelled`.

#### 5. `timeout` — `[Test] Provider Timeout -> Reconciles to Confirmed`
- **Flow**: `Pending` → `Reserving` → `Paying` → **`PaymentUnknown`** *(pauses ~8s)* → `Committing` → `Shipping` → **`Confirmed`**
- **What happens**:
  - Simulates a gateway where the charge succeeded at the bank, but the HTTP response timed out.
  - `CollectPaymentConsumer` sets `PaymentAttempt.Status = PaymentUnknown` (`3`) and publishes `PaymentPendingResolution`.
  - `Order` enters **`PaymentUnknown`** and displays the amber `Timeout = Unknown, Not Failure` banner.
  - Within ~8 seconds, `PaymentReconciliationWorker` (`Payment.Worker`) queries `GetStatusAsync`, discovers the charge succeeded (`mock_tx_<attemptId>`), marks the attempt `Succeeded`, and publishes `PaymentSucceeded`.
  - The Saga resumes automatically (`Committing` → `Shipping` → **`Confirmed`**).

#### 6. `network_error` — `[Test] Network Error -> Reconciles to Cancelled`
- **Flow**: `Pending` → `Reserving` → `Paying` → **`PaymentUnknown`** *(pauses ~8s)* → **`Cancelled`**
- **What happens**:
  - Simulates a connection drop where the request never reached the payment processor.
  - Initially enters **`PaymentUnknown`** (`Timeout = Unknown`).
  - Within ~8 seconds, `PaymentReconciliationWorker` queries the provider, confirms the transaction failed (`Failed`), and publishes `PaymentRejected`.
  - `CheckoutStateMachine` transitions the order from `PaymentUnknown` to **`Cancelled`** and releases the reserved inventory hold.

---

### Monitoring & Validation Guide for Each Step & Payment Scenario

#### 1. Step-by-Step Lifecycle Validation Checklist

| Saga Stage | UI Indicator | SQL Server Validation | RabbitMQ Validation (`:15672`) |
|------------|-------------|-----------------------|--------------------------------|
| **Cart Setup** | Cart drawer shows items & verified quote total | *(Stored in Redis, not SQL)* | *(No messages yet)* |
| **Order Submitted (`Pending` → `Reserving`)** | Toast `Order Submitted`; Receipt / Tracker opens | `ShopSphere_Order.order.Orders`: `Status = 'Reserving'` (or `'Paying'`)<br>`ShopSphere_Order.order.CheckoutSagaStates`: `CurrentState = 'Reserving'` | `reserve-inventory` queue receives 1 message |
| **Stock Reserved (`Paying`)** | Saga visualizer highlights `Paying` | `ShopSphereInventory.dbo.StockItems`: `QuantityAvailable` decreased, `QuantityHeld` increased<br>`ShopSphereInventory.dbo.StockReservations`: `Status = 1` (`Held`) | `collect-payment` queue receives 1 message |
| **Payment Succeeded (`Committing` → `Shipping`)** | Saga advances toward fulfillment | `ShopSpherePayment.dbo.PaymentAttempts`: `Status = 2` (`Succeeded`), `ProviderTransactionId` populated | `commit-inventory` and `create-shipment` queues process 1 message each |
| **Order Confirmed (`Confirmed`)** | Green `Order Confirmed` badge + Tracking Number (`SS-TRK-...` / `GHN...`) + Email Preview card | `ShopSphere_Order.order.Orders`: `Status = 'Confirmed'`, `TrackingNumber` set<br>`ShopSphereInventory.dbo.StockReservations`: `Status = 2` (`Committed`)<br>`ShopSphere_Shipping.shipping.Shipments`: `Status = 'LabelCreated'`<br>`ShopSphere_Notification.notification.NotificationJobs`: `Status = 'Sent'` | All queues show `0 Ready`, `0 Unacked`, and no `*_error` queues created |

#### 2. Validating Each Payment Scenario

| Scenario | How to Trigger in UI | Expected Intermediate State | Expected Final State | Key Database Verification (`ShopSpherePayment` & `ShopSphereInventory`) |
|----------|----------------------|-----------------------------|----------------------|-------------------------------------------------------------------------|
| **1. Stripe / Mock Card (`credit_card`)** | Select **Credit or Debit Card** → Place Order | `Reserving` → `Paying` | **`Confirmed`** (`USD`) | `PaymentAttempts.Status = 2` (`Succeeded`), `StockReservations.Status = 2` (`Committed`) |
| **2. MoMo / VNPay (`digital_wallet` / `bank_transfer`)** | Select **MoMo E-Wallet** or **VNPay** → Place Order | `Reserving` → `Paying` (`FX: 1 USD = 25,400 VND`) | **`Confirmed`** (`VND`) | `Orders.Currency = 'VND'`, `Orders.TotalAmount = USD * 25400`, `PaymentAttempts.Currency = 'VND'` |
| **3. Declined (`decline`)** | Select **`[Test] Declined Card`** → Place Order | `Reserving` → `Paying` | **`Cancelled`** (Immediate) | `PaymentAttempts.Status = 4` (`Failed`), `StockReservations.Status = 3` (`Released`), `QuantityHeld = 0` |
| **4. Timeout (`timeout`)** | Select **`[Test] Provider Timeout`** → Place Order | **`PaymentUnknown`** (Amber banner for ~8s) | **`Confirmed`** (Auto-reconciles in ~8s) | Initially `PaymentAttempts.Status = 3` (`PaymentUnknown`); after `PaymentReconciliationWorker` sweep → `Status = 2` (`Succeeded`) & `StockReservations.Status = 2` (`Committed`) |
| **5. Network Error (`network_error`)** | Select **`[Test] Network Error`** → Place Order | **`PaymentUnknown`** (Amber banner for ~8s) | **`Cancelled`** (Auto-reconciles in ~8s) | Initially `PaymentAttempts.Status = 3` (`PaymentUnknown`); after `PaymentReconciliationWorker` sweep → `Status = 4` (`Failed`) & `StockReservations.Status = 3` (`Released`) |

#### 3. Quick SQL & Redis Inspection Commands

Run these commands in PowerShell at any point during or after checkout to inspect live state across all databases:

```powershell
# 1. Inspect latest Orders & Saga State (ShopSphere_Order)
docker exec shopsphere-sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "DevPassword123!" -d ShopSphere_Order -Q "SELECT TOP 5 Id, Status, PaymentMethod, TotalAmount, Currency, PaymentAttemptId, TrackingNumber, FailureReason FROM [order].[Orders] ORDER BY CreatedAtUtc DESC;" -W

# 2. Inspect Stock Levels & Reservations (ShopSphereInventory)
docker exec shopsphere-sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "DevPassword123!" -d ShopSphereInventory -Q "SELECT Sku, QuantityAvailable, QuantityHeld FROM StockItems; SELECT TOP 5 OrderId, Sku, Quantity, Status, ExpiresAtUtc FROM StockReservations ORDER BY CreatedAtUtc DESC;" -W

# 3. Inspect Payment Attempts (ShopSpherePayment)
# Status enum: 1 = Pending, 2 = Succeeded, 3 = PaymentUnknown, 4 = Failed, 5 = Refunded
docker exec shopsphere-sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "DevPassword123!" -d ShopSpherePayment -Q "SELECT TOP 5 Id, OrderId, PaymentMethod, Amount, Currency, Status, ProviderTransactionId, FailureReason FROM PaymentAttempts ORDER BY CreatedAtUtc DESC;" -W

# 4. Inspect Shipments (ShopSphere_Shipping)
docker exec shopsphere-sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "DevPassword123!" -d ShopSphere_Shipping -Q "SELECT TOP 5 Id, OrderId, Status, TrackingNumber, ExternalShipmentRequestId FROM [shipping].[Shipments] ORDER BY CreatedAtUtc DESC;" -W

# 5. Inspect Notification Dispatches (ShopSphere_Notification)
docker exec shopsphere-sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "DevPassword123!" -d ShopSphere_Notification -Q "SELECT TOP 5 Id, OrderId, NotificationType, Channel, Status, Recipient FROM [notification].[NotificationJobs] ORDER BY CreatedAtUtc DESC;" -W

# 6. Inspect Redis Cart Keys
docker exec shopsphere-redis redis-cli KEYS "ShopSphere_Cart:*"
```

---

## One-Time Manual Steps

These require human interaction and cannot be automated:

### GitHub Authentication

For GitHub MCP server (PR/issue management):

```bash
gh auth login
```

Follow prompts to authenticate via browser or token.

### Notion Integration

For Notion MCP server (docs/specs):

1. Go to https://www.notion.so/my-integrations
2. Click "+ New integration"
3. Name: "ShopSphere Docs"
4. Associated workspace: Select your workspace
5. Capabilities: Read content, Update content, Insert content
6. Submit → Copy "Internal Integration Secret" (starts with `secret_`)
7. Add to `.env`: `NOTION_TOKEN=secret_...`
8. **Important**: Go to Notion pages/databases you want accessible, click **Share**, invite the integration by name

## Token Creation

### GitHub Fine-Grained Personal Access Token

Use a fine-grained personal access token with least-privilege permissions scoped only to this repository:

1. Visit https://github.com/settings/personal-access-tokens/new
2. Token name: "ShopSphere AI Agent"
3. Expiration: 90 days (rotate regularly)
4. Repository access: **Only select repositories** → Select `ShopSphere`
5. Repository permissions:
   - ✅ **Contents**: Read and write (for code inspection and branch commits)
   - ✅ **Pull requests**: Read and write (for PR creation and review)
   - ✅ **Issues**: Read and write (for task tracking)
   *(Note: `workflow` permission is dropped for security unless managing CI workflows)*
6. Generate token → Copy (starts with `github_pat_` or classic `ghp_`)
7. Add to `.env`: `GITHUB_TOKEN=github_pat_...`

### Notion Integration Token

See [Notion Integration](#notion-integration) above.

## Architecture

ShopSphere is a microservices e-commerce platform with:

- **8 Services**: Identity, Catalog, Cart, Order, Inventory, Payment, Shipping, Notification
- **Saga Orchestration**: Checkout workflow coordinates stock reservation → payment → shipping → notification
- **Transactional Outbox**: Atomic writes + messaging, no lost/duplicate messages
- **Eventual Consistency**: Services update asynchronously, expose intermediate states
- **Resilience Patterns**: Retries, circuit breakers, timeout = unknown (not failure)

**Key Principles**:
- Each service owns its data (no cross-service DB access)
- Redis caches reads only (not source of truth)
- Concurrency control via EF Core tokens
- Late events check state/version before applying

See [AGENTS.md](./AGENTS.md) for complete architectural rules.

## Project Structure

```
ShopSphere/
├── src/
│   ├── BuildingBlocks/      # Shared cross-cutting concerns (Security, Results, Outbox)
│   ├── Gateway/             # YARP reverse proxy API gateway
│   ├── Services/            # Microservices (Identity, Catalog, Cart, Inventory, Payment, Order, Shipping, Notification)
│   └── Web/                 # React SPA (Customer Storefront & Admin Operations Portal)
├── tests/                   # Unit and integration test suites
├── deploy/
│   ├── compose/             # Local Docker infra (SQL Server, RabbitMQ, Redis)
│   └── terraform/           # IaC modules
├── docs/
│   ├── requirements/        # Architecture, services, messaging, infra
│   ├── adr/                 # Architecture Decision Records
│   ├── specs/               # Service OpenAPI specifications
│   ├── CREDENTIALS.md       # Local development credentials & connection strings
│   └── agent-setup.md       # MCP configuration guide
├── scripts/
│   ├── bootstrap.sh         # First-run setup
│   ├── start-dev.ps1 / .sh  # Single-command local launcher for all services
│   ├── doctor.sh / .ps1     # Environment health checks
│   └── lint.sh / .ps1       # Code formatting and lint verification
└── AGENTS.md                # Architecture + workflow rules (<150 lines)
```

## Technology Stack

| Layer | Technology |
|-------|-----------|
| **Backend** | .NET 10 LTS, ASP.NET Core, EF Core |
| **Frontend** | React 18, TypeScript, Vite, YARP gateway |
| **Messaging** | MassTransit 8.3.3, RabbitMQ 3.13 |
| **Data** | SQL Server 2022, Redis 7 |
| **Testing** | xUnit, FluentAssertions, Testcontainers |
| **Observability** | OpenTelemetry, Jaeger, Grafana (local) |
| **IaC** | Terraform |

## Development Workflow

1. **Check requirements**: Review `docs/requirements/` for patterns
2. **Write spec** (if new feature): API, messages, state transitions
3. **Implement**: Follow service boundaries, use Transactional Outbox
4. **Test**: Unit + integration, **include failure paths**
5. **Run doctor**: `bash scripts/doctor.sh` (or `pwsh scripts/doctor.ps1`) before committing
6. **Commit**: Use [Conventional Commits](https://www.conventionalcommits.org/)
7. **PR**: Use template checklist (architecture, testing, verification)

## Agent Support

AI agents configured via MCP (Model Context Protocol):

- **Claude Code**: Auto-configured via `.mcp.json`
- **Antigravity**: Centralized MCP configuration (see [docs/agent-setup.md](./docs/agent-setup.md))

All agents have access to:
- GitHub PR/issue management
- Notion docs/specs
- Terraform inspection (read-only)
- Codebase context indexing
- Microsoft Learn docs

## MCP Servers

| Server | Purpose |
|--------|---------|
| **github** | PR creation, issue management, repo operations |
| **notion** | Docs/specs management |
| **context7** | Codebase context indexing |
| **microsoft-learn** | .NET/Azure documentation |
| **terraform** | Infrastructure inspection (read-only) |

## Useful Commands

```bash
# Health check
bash scripts/doctor.sh

# Start all infrastructure + services + frontend
pwsh scripts/start-dev.ps1

# Start infrastructure only
docker compose -f deploy/compose/docker-compose.yml up -d

# Check infrastructure status
docker compose -f deploy/compose/docker-compose.yml ps

# Stop infrastructure
docker compose -f deploy/compose/docker-compose.yml down

# View logs
docker compose -f deploy/compose/docker-compose.yml logs -f [service]

# Format C# code
dotnet format

# Run tests
dotnet test

# Terraform plan (dev environment)
cd deploy/terraform/environments/dev
terraform plan
```

## Troubleshooting

### Dev container fails to build

```bash
# Rebuild without cache
Ctrl+Shift+P → "Dev Containers: Rebuild Container"
```

### Docker Compose health checks fail

```bash
# Check container logs
docker compose -f deploy/compose/docker-compose.yml logs [sqlserver|rabbitmq|redis]

# Restart infrastructure
docker compose -f deploy/compose/docker-compose.yml restart
```

### MCP servers not working

1. Check tokens in `.env`
2. Restart IDE
3. Verify MCP config syntax: `cat .mcp.json | jq`
4. See detailed troubleshooting in [docs/agent-setup.md](./docs/agent-setup.md#troubleshooting)

### Skills not found

#### Symlink Method (Recommended)

```bash
# Git Bash / WSL
ln -s ../.agents/skills .claude/skills

# PowerShell (requires Developer Mode or admin)
cmd /c mklink /D .claude\skills .agents\skills
```

#### Fallback: Sync Script (Windows)

If symlinks fail:

```bash
pwsh scripts/sync-skills.ps1
```

Re-run on every skill update. Or add to `bootstrap.sh`.

## Contributing

1. Read [AGENTS.md](./AGENTS.md) for architecture rules
2. Check [docs/adr/](./docs/adr/) for existing decisions
3. Write spec for new features in `docs/specs/`
4. Follow the PR template checklist
5. Include tests for failure paths (out of stock, payment timeout, late events)

## Resources

- **Architecture Guide**: [AGENTS.md](./AGENTS.md)
- **Credentials & Endpoints**: [docs/CREDENTIALS.md](./docs/CREDENTIALS.md)
- **Requirements**: [docs/requirements/](./docs/requirements/)
- **ADRs**: [docs/adr/](./docs/adr/)
- **Agent Setup**: [docs/agent-setup.md](./docs/agent-setup.md)
- **MassTransit Docs**: https://masstransit.io/
- **EF Core Docs**: https://learn.microsoft.com/en-us/ef/core/
- **ASP.NET Core Docs**: https://learn.microsoft.com/en-us/aspnet/core/

## License

This project is licensed under the MIT License - see the [LICENSE](./LICENSE) file for details.

## Support

For questions or issues:
- Check [docs/agent-friction.md](./docs/agent-friction.md) for common pitfalls
- Review architecture in [docs/requirements/](./docs/requirements/)
- Open an issue with the `question` label

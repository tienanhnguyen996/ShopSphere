# ShopSphere Local Development Credentials & Endpoints

> ⚠️ **For Local Development & Testing Only**: None of the values in this document are production secrets. Never reuse local development passwords or keys in staging or production environments.

---

## 1. Docker Infrastructure (`deploy/compose/docker-compose.yml`)

| Component | Container Name | Host / Port | Username | Password | Management / Notes |
|-----------|---------------|-------------|----------|----------|--------------------|
| **SQL Server 2022** | `shopsphere-sqlserver` | `localhost,1433` | `sa` | `DevPassword123!` | `TrustServerCertificate=True` |
| **RabbitMQ 3.13** | `shopsphere-rabbitmq` | `localhost:5672` (AMQP)<br>`http://localhost:15672` (UI) | `guest` | `guest` | Virtual Host: `/` |
| **Redis 7** | `shopsphere-redis` | `localhost:6379` | *(none)* | *(none)* | Default DB `0` (Cart prefix: `ShopSphere_Cart:`) |

---

## 2. SQL Server Databases (Per-Service Boundaries)

Each service owns its dedicated database on `localhost,1433` (`User Id=sa;Password=DevPassword123!;TrustServerCertificate=True;`):

| Service | Port | Database Name | Schema | Connection String Key |
|---------|------|---------------|--------|-----------------------|
| **CatalogService** | `5001` | `ShopSphereCatalog` | `dbo` | `ConnectionStrings:DefaultConnection` |
| **CartService** | `5002` | *(Redis `localhost:6379`)* | `cart:{customerId}` | `ConnectionStrings:Redis` |
| **InventoryService** | `5003` | `ShopSphereInventory` | `dbo` | `ConnectionStrings:DefaultConnection` |
| **PaymentService** | `5004` | `ShopSpherePayment` | `dbo` | `ConnectionStrings:PaymentDatabase` |
| **OrderService** | `5005` | `ShopSphere_Order` | `order` | `ConnectionStrings:OrderDb` |
| **ShippingService** | `5006` | `ShopSphere_Shipping` | `shipping` | `ConnectionStrings:ShippingDb` |
| **IdentityService** | `5007` | `ShopSphereIdentity` | `dbo` | `ConnectionStrings:IdentityDatabase` |
| **NotificationService** | `5008` | `ShopSphere_Notification` | `notification` | `ConnectionStrings:NotificationDb` |

---

## 3. Default Seeded Identity Account (`IdentityDbSeeder`)

| Role | Email | Password | User ID |
|------|-------|----------|---------|
| **Admin** (`SuperAdmin`) | `admin@shopsphere.local` | `AdminPassword123!` | `11111111-1111-1111-1111-111111111111` |

---

## 4. Application & API Authentication / Headers

| Setting / Header | Default Local Value | Used By |
|------------------|---------------------|---------|
| **Customer Header** (`X-Customer-Id`) | `cust-alex-01` (or any non-empty string/UUID) | `Cart.Api`, `Order.Api`, `Web` |
| **Idempotency Header** (`Idempotency-Key`) | Any unique string/UUID per mutating request | `Order.Api`, `Shipping.Api` |
| **Correlation Header** (`X-Correlation-Id`) | Auto-generated UUID v4 per workflow | All APIs, Gateway, and Frontend |
| **Payment Webhook Secret** (`Payment:WebhookSecret`) | `ShopSpherePaymentWebhookSecret2026` | `POST /api/v1/payment/webhooks/mock-provider` (`X-Payment-Signature` HMAC-SHA256) |
| **JWT Signing Key** (`JwtSettings:SecretKey`) | `super-secret-development-key-that-is-at-least-32-bytes!` | `BuildingBlocks.Security` (`Issuer`: `ShopSphere.Identity`, `Audience`: `ShopSphere.Services`) |

---

## 5. Sandbox Payment & Shipping Provider Credentials

### Payment Providers (`PaymentSettings` in `Payment.Api/appsettings.json`)

- **Hardcoded Currency Exchange**: `1 USD = 25,400 VND` (applied automatically when checking out with `digital_wallet` / MoMo or `bank_transfer` / VNPay).
- **`FallbackToMockOnMissingCredentials`**: `true` (automatically falls back to `MockPaymentProviderClient` when a provider API key is empty).

| Provider | Payment Method Values | Default Sandbox Config |
|----------|-----------------------|------------------------|
| **Stripe Sandbox** | `credit_card`, `stripe`, `card` | `BaseUrl`: `https://api.stripe.com/v1/` (`ApiKey`: empty by default → falls back to Mock; set `sk_test_...` to call Stripe directly) |
| **MoMo Sandbox (VND)** | `digital_wallet`, `momo`, `ewallet` | `PartnerCode`: `MOMOBKUN20180529`<br>`AccessKey`: `klm05TvNBzhg7h7j`<br>`SecretKey`: `at67qH6mk8w5Y1nAyMoYKMWACiEi2bsa`<br>`BaseUrl`: `https://test-payment.momo.vn/v2/gateway/api/` |
| **VNPay Sandbox (VND)** | `bank_transfer`, `vnpay`, `banking` | `TmnCode`: `DEMOVNPAY`<br>`HashSecret`: `RAZUIDIBAARAYOPUPFDICPHGKWUZGPUT`<br>`BaseUrl`: `https://sandbox.vnpayment.vn/` |
| **Mock Simulation Modes** | `decline`, `timeout`, `network_error` | Routed directly to `MockPaymentProviderClient` to test Saga failure, timeout (`PaymentUnknown`), and reconciliation paths |

### Shipping Carriers (`CarrierSettings` in `Shipping.Api/appsettings.json`)

| Carrier | Destination Country Trigger | Default Sandbox Config |
|---------|----------------------------|------------------------|
| **Shippo Sandbox** | International / `US` | `BaseUrl`: `https://api.goshippo.com/` (`ApiKey`: empty by default → falls back to `MockCarrierProviderClient`) |
| **GHN Sandbox** | `VN` / `VIETNAM` | `BaseUrl`: `https://dev-online-gateway.ghn.vn/shiip/public-api/v2/` (`Token` / `ShopId`: empty by default → falls back to `MockCarrierProviderClient`) |
| **Mock Carrier Hooks** | Postal code `00000-TIMEOUT` or `00000-FAIL` | Simulates `ShipmentUnknown` timeout or `ShipmentFailed` compensation |

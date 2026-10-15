# Cart Service Specification

## 1. Overview & Service Boundaries
The Cart Service manages customer shopping carts, item quantities, and checkout price quotes.

- **Boundary**: The cart is a temporary user convenience store. It is **never** an inventory commitment or reservation.
- **Source of Truth**: Redis is used for high-throughput, low-latency cart storage keyed by customer ID with a 14-day sliding TTL.
- **Downstream Dependency**: Calls Catalog Service over HTTP to verify live product active status, unit prices, and price versions.
- **Upstream Consumer**: Order Service and Frontend Web App consume cart and checkout quotes.

```
+-------------+         HTTP (GET /api/v1/cart)        +------------------+
| Frontend /  | -------------------------------------> |   Cart Service   |
| API Gateway | <------------------------------------- | (Redis Storage)  |
+-------------+                                        +------------------+
                                                                |
                                                                | HTTP (Resilient)
                                                                v
                                                       +------------------+
                                                       | Catalog Service  |
                                                       +------------------+
```

## 2. Domain Model & Invariants

### 2.1 Cart Aggregate (`Cart.Domain`)
- `CustomerId` (`string` or `Guid`, non-empty)
- `Items` (`IReadOnlyCollection<CartItem>`)
- `CreatedAtUtc` (`DateTime`)
- `LastModifiedUtc` (`DateTime`)

### 2.2 CartItem
- `Sku` (`string`, alphanumeric, uppercase format `^[A-Z0-9-_]+$`)
- `Quantity` (`int`, invariant: `1 <= Quantity <= 99`)

### 2.3 Aggregate Methods & Rules
- `AddItem(string sku, int quantity)`:
  - If SKU already exists in cart, increments quantity: `existing.Quantity + quantity`.
  - Invariant: combined quantity cannot exceed `99`. If it does, throws `DomainValidationException` / fails validation.
- `UpdateItemQuantity(string sku, int quantity)`:
  - If `quantity <= 0`, removes item from cart.
  - If `quantity > 99`, rejected with validation error.
  - Otherwise updates item quantity.
- `RemoveItem(string sku)`: Removes item with matching SKU.
- `Clear()`: Clears all items in the cart.

## 3. Storage & Redis Repository (`Cart.Infrastructure`)

### 3.1 Contract: `ICartRepository`
```csharp
public interface ICartRepository
{
    Task<Cart?> GetCartAsync(string customerId, CancellationToken ct = default);
    Task<Cart> SaveCartAsync(Cart cart, CancellationToken ct = default);
    Task<bool> ClearCartAsync(string customerId, CancellationToken ct = default);
}
```

### 3.2 Redis Storage Strategy
- **Key Convention**: `cart:{customerId}`
- **TTL**: 14-day sliding expiration (`TimeSpan.FromDays(14)`).
- **Serialization**: System.Text.Json with camelCase / ISO-8601 timestamps.
- **Resilience / Fallback**: If Redis read fails (connection timeout, redis server unreachable), log a warning and return an empty `Cart(customerId)` rather than crashing the client experience.

## 4. Catalog HTTP Client & Price Quote Handler (`Cart.Application`)

### 4.1 Contract: `ICatalogClient`
```csharp
public interface ICatalogClient
{
    Task<Result<CatalogProductDto>> GetProductBySkuAsync(string sku, CancellationToken ct = default);
}
```

### 4.2 Query: `GetCheckoutQuoteQuery`
- **Request**: `CustomerId`
- **Response**: `CheckoutQuoteDto`
  - `CustomerId`: string
  - `Items`: List of `CheckoutQuoteItemDto`:
    - `Sku`: string
    - `ProductName`: string
    - `Quantity`: int
    - `UnitPrice`: decimal
    - `LineTotal`: decimal
    - `Currency`: string
    - `PriceVersion`: int
    - `IsActive`: bool
  - `Subtotal`: decimal
  - `Currency`: string
  - `PriceVersions`: Dictionary<string, int> (mapping SKU -> PriceVersion for OrderService snapshot validation)
  - `IsValid`: bool
  - `ValidationErrors`: List<string>

### 4.3 Pricing Verification Logic
1. Load customer cart from repository. If empty, return empty quote with `Subtotal = 0, IsValid = true`.
2. For each item in cart, query `ICatalogClient.GetProductBySkuAsync(item.Sku)`:
   - If Catalog Service returns error / unavailable:
     - Return `Result.Failure(Error.Custom("Cart.CatalogUnavailable", "Catalog service is currently unavailable.", ErrorType.Failure))`.
   - If SKU not found in Catalog:
     - Mark quote `IsValid = false`, add error `"Product '{sku}' is no longer available in the catalog."`.
   - If product status is not `Active` (e.g. `Discontinued` or `Draft`):
     - Mark quote `IsValid = false`, add error `"Product '{sku}' is no longer active for purchase."`.
3. Compute `LineTotal = item.Quantity * product.Price`.
4. Ensure currency consistency across all cart items.
5. Populate `PriceVersions[item.Sku] = product.PriceVersion`.
6. Return `CheckoutQuoteDto`.

## 5. Web API Endpoints (`Cart.Api`)

All cart endpoints require the `X-Customer-Id` HTTP header.

| Method | Route | Description | Success Code |
|---|---|---|---|
| `GET` | `/health` | Health checks for Redis and Catalog connectivity | 200 OK / 503 |
| `GET` | `/api/v1/cart` | Get current customer's shopping cart | 200 OK |
| `POST` | `/api/v1/cart/items` | Add or increment item in shopping cart | 200 OK |
| `PUT` | `/api/v1/cart/items/{sku}` | Update quantity of specific SKU in cart | 200 OK |
| `DELETE` | `/api/v1/cart/items/{sku}` | Remove item from cart | 204 No Content |
| `DELETE` | `/api/v1/cart` | Clear entire shopping cart | 204 No Content |
| `GET` | `/api/v1/cart/quote` | Generate verified checkout price quote | 200 OK |

## 6. Failure Paths & HTTP Status Codes

| Scenario | HTTP Status | Error Code | Behavior |
|---|---|---|---|
| Missing `X-Customer-Id` | 400 Bad Request | `Cart.CustomerIdRequired` | Rejects with message requiring Customer ID |
| Item quantity > 99 | 400 Bad Request | `Cart.QuantityExceeded` | Rejects mutation, items unchanged |
| Redis down on read | 200 OK | Fallback | Logs warning, returns empty cart fallback |
| Catalog service down during quote | 503 Service Unavailable | `Cart.CatalogUnavailable` | Returns 503 with retry-after recommendation |
| Discontinued item during quote | 200 OK | N/A | Quote returns `IsValid = false` with item-level error |

# ShopSphere Code Review Report
**Date**: 2026-10-16  
**Scope**: Payment, Inventory, Cart, and Catalog services  
**Lines Analyzed**: ~4,200 across 28 files

## Executive Summary

This review identified **12 findings** across the ShopSphere monorepo, with **5 critical issues** requiring immediate attention. The most severe violations involve the Inventory Service publishing messages before database commits (violating transactional outbox pattern) and the Cart Service using Redis as authoritative storage without SQL backing.

**Risk Summary**:
- 🔴 **Critical**: 5 issues (data loss, phantom events, race conditions)
- 🟠 **High**: 4 issues (duplicate charges, stuck workflows, 500 errors)
- 🟡 **Medium**: 2 issues (test coverage gaps)
- 🟢 **Low**: 1 observation (correctly implemented)

---

## Critical Issues (Immediate Fix Required)

### 1. Inventory Service Violates Transactional Outbox Pattern 🔴

**Severity**: Critical  
**Impact**: Message loss, phantom events, inconsistent state across services

**Problem**:
All four Inventory consumers publish messages BEFORE `SaveChangesAsync()`, violating the fundamental outbox pattern. If the database commit fails, downstream services still receive the event.

**Affected Files**:
- `src/Services/Inventory/Inventory.Application/Consumers/ReserveInventoryConsumer.cs:138-141`
- `src/Services/Inventory/Inventory.Application/Consumers/CommitInventoryConsumer.cs:120-121, 134-135`
- `src/Services/Inventory/Inventory.Application/Consumers/ReleaseInventoryConsumer.cs:89-90`
- `src/Services/Inventory/Inventory.Application/Workers/ReservationExpiryWorker.cs:122-125`

**Current Code** (ReserveInventoryConsumer example):
```csharp
await publishEndpoint.Publish(successEnvelope, ct);
await _dbContext.SaveChangesAsync(ct);  // If this fails, message already sent!
```

**Impact Scenario**:
1. ReserveInventoryConsumer reserves 5 units, publishes `InventoryReserved`
2. `SaveChangesAsync()` throws (deadlock, constraint violation)
3. Order Service receives `InventoryReserved`, transitions order to `Reserving → PaymentPending`
4. Inventory transaction rolls back — stock never actually reserved
5. Order proceeds to payment with phantom reservation

**Recommended Fix**:
```csharp
// Save FIRST (commits business data + outbox message atomically)
await _dbContext.SaveChangesAsync(ct);

// THEN publish (MassTransit outbox relay will deliver)
await publishEndpoint.Publish(successEnvelope, ct);
```

**Priority**: 🚨 Immediate (Block all other work until fixed)

---

### 2. Cart Service Uses Redis as Authoritative Storage 🔴

**Severity**: Critical  
**Impact**: Data loss on Redis failure, no transactional consistency, no audit trail

**Problem**:
`RedisCartRepository` stores cart data exclusively in Redis (IDistributedCache) with 14-day sliding expiration. No SQL backing exists.

**Affected File**:
- `src/Services/Cart/Cart.Infrastructure/Repositories/RedisCartRepository.cs` (entire file)

**Current Implementation**:
```csharp
public async Task SaveAsync(Cart cart, CancellationToken ct = default)
{
    var json = JsonSerializer.Serialize(cart, _options);
    var key = GetCartKey(cart.UserId);
    await _cache.SetStringAsync(key, json, new DistributedCacheEntryOptions
    {
        SlidingExpiration = TimeSpan.FromDays(14)
    }, ct);
}
```

**Violations**:
- **AGENTS.md**: "Redis is cache, not source of truth"
- **01-architecture.md**: "Each service owns its data, database/schema, and business rules"
- No atomicity with other operations (e.g., reserve inventory + update cart)
- No concurrency control
- No domain events → no cart abandonment workflow possible

**Impact Scenario**:
1. User adds 10 items to cart over 30 minutes
2. Redis instance fails (memory pressure, network partition, pod restart)
3. All cart data lost — user sees empty cart
4. Customer support has no audit trail to restore cart
5. Lost revenue opportunity

**Recommended Fix**:
1. Add `CartDbContext` with EF Core
2. Implement `EfCartRepository` with SQL storage
3. Keep Redis for read-model caching only
4. Add `RowVersion` to Cart aggregate for optimistic locking
5. Implement transactional outbox for `CartItemAdded`, `CartItemRemoved`, `CartCheckedOut` events

**Priority**: 🚨 Immediate (This sprint, pair with #3 and #4)

---

### 3. Cart Service Has No Concurrency Control 🔴

**Severity**: Critical  
**Impact**: Silent data loss on concurrent updates (last write wins)

**Problem**:
`Cart` aggregate lacks `RowVersion` property. When two requests modify the same cart concurrently, the last write silently overwrites the first.

**Affected File**:
- `src/Services/Cart/Cart.Domain/Cart.cs` (missing RowVersion property)

**Current Code**:
```csharp
public class Cart
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; }
    public List<CartItem> Items { get; private set; }
    // ❌ No RowVersion property
}
```

**Impact Scenario**:
1. User has cart with Item A (qty 1)
2. Request 1: Add Item B (qty 2) → reads cart, adds B, writes `{A:1, B:2}`
3. Request 2: Update Item A qty to 5 → reads cart (still `{A:1}`), updates, writes `{A:5}`
4. Final state: `{A:5}` — Item B silently lost

**Recommended Fix**:
```csharp
public class Cart
{
    public Guid Id { get; private set; }
    public string UserId { get; private set; }
    public List<CartItem> Items { get; private set; }
    
    [Timestamp]  // Maps to SQL Server rowversion
    public byte[] RowVersion { get; set; }
}
```

**Also Required**:
- Handle `DbUpdateConcurrencyException` in command handlers
- Return 409 Conflict to frontend
- Frontend retry logic or "Another device modified your cart" notification

**Priority**: 🚨 Immediate (Bundle with #2 migration to SQL)

---

### 4. Cart Service Has No Transactional Outbox 🔴

**Severity**: Critical  
**Impact**: Other services cannot react to cart changes, no event-driven workflows

**Problem**:
Cart mutations (AddItem, RemoveItem, UpdateQuantity, CheckoutCart) save to Redis without publishing domain events. No outbox pattern configured.

**Affected Files**:
- `src/Services/Cart/Cart.Application/Carts/Commands/AddItem/AddItemToCartCommand.cs`
- All other Cart command handlers

**Current Code**:
```csharp
public async Task<Result<CartDto>> Handle(AddItemToCartCommand request, CancellationToken ct)
{
    var cart = await _cartRepository.GetByUserIdAsync(request.UserId, ct) 
               ?? Cart.Create(request.UserId);
    
    cart.AddItem(productId, quantity, price);
    
    await _cartRepository.SaveAsync(cart, ct);  // ❌ No events published
    
    return Result.Success(cart.ToDto());
}
```

**Missing Events**:
- `CartItemAdded` → Marketing service could trigger "You left items in your cart" email
- `CartCheckedOut` → Analytics service could track conversion funnel
- `CartAbandoned` → CRM service could send recovery email after 24 hours

**Recommended Fix**:
1. Add `CartDbContext` with MassTransit outbox:
```csharp
services.AddDbContext<CartDbContext>(...);
services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<CartDbContext>(o =>
    {
        o.UseSqlServer();
        o.UseBusOutbox();
    });
});
```

2. Publish events after `SaveChangesAsync`:
```csharp
await _dbContext.SaveChangesAsync(ct);  // Commits cart data + outbox
await publishEndpoint.Publish(new CartItemAdded { ... }, ct);
```

**Priority**: 🚨 Immediate (Bundle with #2 SQL migration)

---

### 5. Frontend Uses Hardcoded Products Instead of API 🔴

**Severity**: Critical  
**Impact**: Catalog service non-functional, users see stale/fake data

**Problem**:
`App.tsx` contains a hardcoded `sampleProducts` array instead of calling `/api/v1/catalog/products` through the gateway.

**Affected File**:
- `src/Web/src/App.tsx:15-45` (sampleProducts array)

**Current Code**:
```typescript
const sampleProducts: Product[] = [
  {
    id: 'prod-001',
    name: 'Ergonomic Mechanical Keyboard',
    price: 129.99,
    // ... 10 more hardcoded products
  }
];
```

**Impact**:
- Catalog Service API exists but unused
- Product changes (price, stock, description) not reflected in UI
- Cannot test real end-to-end workflows
- Frontend team developing against stale data

**Recommended Fix**:
1. Generate TypeScript client from Catalog OpenAPI spec:
```bash
npx openapi-typescript-codegen --input http://localhost:5001/swagger/v1/swagger.json --output src/generated/catalog-api
```

2. Replace hardcoded array with API call:
```typescript
import { ProductsService } from './generated/catalog-api';

function App() {
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  
  useEffect(() => {
    ProductsService.getProducts()
      .then(setProducts)
      .finally(() => setLoading(false));
  }, []);
  
  if (loading) return <LoadingSpinner />;
  // ... rest of component
}
```

**Priority**: 🚨 Immediate (This sprint, unblocks frontend testing)

---

## High Priority Issues (This Sprint)

### 6. Payment Service Missing MessageId-Based Idempotency 🟠

**Severity**: High  
**Impact**: Duplicate charges on message redelivery

**Problem**:
`CollectPaymentConsumer` checks `OrderId` uniqueness but doesn't validate `MessageId`, allowing duplicate processing if the same message is redelivered with a different `MessageId`.

**Affected File**:
- `src/Services/Payment/Payment.Application/Consumers/CollectPaymentConsumer.cs:47-87`

**Current Code**:
```csharp
var existingAttempt = await _dbContext.PaymentAttempts
    .FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

if (existingAttempt is not null)
{
    _logger.LogWarning("Payment attempt already exists for Order {OrderId}", orderId);
    return;  // ❌ Only checks OrderId, not MessageId
}
```

**Impact Scenario**:
1. `CollectPayment` message (MessageId: `msg-001`) processed → charge $100
2. RabbitMQ redelivers same message with new MessageId: `msg-002` (network blip, broker restart)
3. Idempotency check passes (different MessageId) → charge another $100
4. Customer sees double charge, disputes, chargeback fees

**Recommended Fix**:
```csharp
// Check BOTH OrderId (operation) and MessageId (message)
var existingAttempt = await _dbContext.PaymentAttempts
    .FirstOrDefaultAsync(p => 
        p.OrderId == orderId || 
        p.IdempotencyKey == message.MessageId.ToString(), 
        ct);

if (existingAttempt is not null)
{
    if (existingAttempt.OrderId == orderId)
        _logger.LogWarning("Payment attempt already exists for Order {OrderId}", orderId);
    else
        _logger.LogWarning("Duplicate message {MessageId} rejected", message.MessageId);
    return;
}

// Store MessageId as idempotency key
var attempt = new PaymentAttempt
{
    OrderId = orderId,
    IdempotencyKey = message.MessageId.ToString(),
    // ...
};
```

**Priority**: 🟠 High (This sprint)

---

### 7. Payment Reconciliation Worker Doesn't Retry Concurrency Conflicts 🟠

**Severity**: High  
**Impact**: Payments stuck in `PaymentUnknown` state indefinitely

**Problem**:
`PaymentReconciliationWorker` catches `DbUpdateConcurrencyException` but only logs a warning without retrying. Concurrent updates leave payment in unknown state.

**Affected File**:
- `src/Services/Payment/Payment.Application/Workers/PaymentReconciliationWorker.cs:145-148`

**Current Code**:
```csharp
try
{
    await _dbContext.SaveChangesAsync(ct);
}
catch (DbUpdateConcurrencyException ex)
{
    _logger.LogWarning(ex, "Concurrency conflict reconciling payment {AttemptId}", attempt.Id);
    // ❌ No retry, payment stays in PaymentUnknown
}
```

**Impact Scenario**:
1. Payment timeout → state `PaymentUnknown`
2. Reconciliation worker queries provider → `PaymentSucceeded`
3. Worker updates state → concurrent update from webhook
4. `DbUpdateConcurrencyException` thrown → logged, ignored
5. Payment remains `PaymentUnknown`, order never ships

**Recommended Fix**:
```csharp
const int MaxRetries = 3;
for (int retry = 0; retry < MaxRetries; retry++)
{
    try
    {
        // Reload fresh state
        var freshAttempt = await _dbContext.PaymentAttempts
            .FirstOrDefaultAsync(p => p.Id == attempt.Id, ct);
        
        if (freshAttempt is null || freshAttempt.Status != PaymentStatus.Unknown)
        {
            _logger.LogInformation("Payment {AttemptId} already resolved", attempt.Id);
            break;
        }
        
        // Apply reconciliation
        freshAttempt.TransitionTo(reconciledStatus);
        await _dbContext.SaveChangesAsync(ct);
        break;  // Success
    }
    catch (DbUpdateConcurrencyException ex) when (retry < MaxRetries - 1)
    {
        _logger.LogWarning(ex, "Concurrency conflict reconciling {AttemptId}, retry {Retry}", 
            attempt.Id, retry + 1);
        await Task.Delay(100 * (retry + 1), ct);  // Exponential backoff
    }
}
```

**Priority**: 🟠 High (This sprint)

---

### 8. Catalog Service Missing Concurrency Exception Handling 🟠

**Severity**: High  
**Impact**: 500 Internal Server Error on concurrent price updates

**Problem**:
`UpdateProductPriceCommandHandler` doesn't catch `DbUpdateConcurrencyException`. Concurrent updates result in unhandled exception → 500 error to client.

**Affected File**:
- `src/Services/Catalog/Catalog.Application/Products/Commands/UpdatePrice/UpdateProductPriceCommandHandler.cs`

**Current Code**:
```csharp
public async Task<Result<ProductDto>> Handle(UpdateProductPriceCommand request, CancellationToken ct)
{
    var product = await _dbContext.Products.FindAsync(request.ProductId, ct);
    
    if (product is null)
        return Result.Failure<ProductDto>("Product not found");
    
    product.UpdatePrice(request.NewPrice);
    
    await _dbContext.SaveChangesAsync(ct);  // ❌ No try-catch for concurrency
    
    return Result.Success(product.ToDto());
}
```

**Impact Scenario**:
1. Admin 1: Update Product X price $99 → $89
2. Admin 2: Update Product X price $99 → $79 (concurrent)
3. Admin 2's save succeeds first → RowVersion updated
4. Admin 1's save fails → `DbUpdateConcurrencyException` → 500 error
5. Admin 1 sees error, confused, doesn't know if update succeeded

**Recommended Fix**:
```csharp
try
{
    await _dbContext.SaveChangesAsync(ct);
    return Result.Success(product.ToDto());
}
catch (DbUpdateConcurrencyException)
{
    _logger.LogWarning("Concurrent update detected for Product {ProductId}", request.ProductId);
    return Result.Failure<ProductDto>(
        "Product was modified by another user. Please refresh and try again.",
        errorCode: "PRODUCT_CONFLICT"
    );
}
```

**Also Required**:
- Frontend: Handle 409 Conflict status
- Show "Product was updated by someone else" toast
- Reload product data and prompt retry

**Priority**: 🟠 High (This sprint)

---

### 9. Frontend Has No OpenAPI-Generated TypeScript Client 🟠

**Severity**: High  
**Impact**: Type safety gaps, manual fetch code, API contract drift

**Problem**:
Frontend makes HTTP calls with manual `fetch()` or hand-written wrappers. No code generation from service OpenAPI specs.

**Current State**:
- Catalog Service has OpenAPI spec at `/swagger/v1/swagger.json`
- Payment, Inventory, Cart services likely have specs too
- Frontend has no generated client, no type safety on API calls

**Impact**:
- Backend adds new required field → frontend doesn't know until runtime
- API endpoint renamed → frontend calls old URL, gets 404
- Response schema changes → TypeScript types don't match, silent failures
- Manual fetch code duplicated across components

**Recommended Fix**:
1. Install code generator:
```bash
npm install --save-dev openapi-typescript-codegen
```

2. Add generation script to `package.json`:
```json
{
  "scripts": {
    "generate:api": "npm run generate:catalog && npm run generate:payment && npm run generate:inventory && npm run generate:cart",
    "generate:catalog": "openapi-typescript-codegen --input http://localhost:5001/swagger/v1/swagger.json --output src/generated/catalog-api",
    "generate:payment": "openapi-typescript-codegen --input http://localhost:5002/swagger/v1/swagger.json --output src/generated/payment-api",
    // ... other services
  }
}
```

3. Use generated client:
```typescript
import { ProductsService } from './generated/catalog-api';

// Before (manual fetch)
const response = await fetch('/api/v1/catalog/products');
const products = await response.json();  // ❌ No types

// After (generated client)
const products = await ProductsService.getProducts();  // ✅ Fully typed
```

**Priority**: 🟠 High (This sprint, pair with #5)

---

## Medium Priority Issues (Next Sprint)

### 10. Integration Tests Missing Testcontainers 🟡

**Severity**: Medium  
**Impact**: Integration tests cannot run in isolation, rely on external Docker Compose

**Problem**:
Payment and Inventory integration test projects reference Testcontainers but don't have the package installed.

**Affected Files**:
- `src/Services/Payment/Payment.IntegrationTests/` (project references Testcontainers but package missing)
- `src/Services/Inventory/Inventory.IntegrationTests/` (same issue)

**Current State**:
- Tests assume SQL Server and RabbitMQ are running on localhost
- Cannot run tests in CI without pre-configured infrastructure
- Developers need Docker Compose running before `dotnet test`

**Recommended Fix**:
1. Install Testcontainers packages:
```bash
dotnet add package Testcontainers.MsSql
dotnet add package Testcontainers.RabbitMq
```

2. Create shared test fixture:
```csharp
public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder().Build();
    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder().Build();
    
    public string ConnectionString => _sqlContainer.GetConnectionString();
    public string RabbitMqUri => _rabbitMqContainer.GetConnectionString();
    
    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
        await _rabbitMqContainer.StartAsync();
    }
    
    public async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
        await _rabbitMqContainer.DisposeAsync();
    }
}
```

3. Use in tests:
```csharp
public class PaymentServiceTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;
    
    public PaymentServiceTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }
    
    [Fact]
    public async Task CollectPayment_Success_PublishesEvent()
    {
        // Arrange
        var dbContext = CreateDbContext(_fixture.ConnectionString);
        var consumer = new CollectPaymentConsumer(dbContext, ...);
        
        // Act & Assert
        // ...
    }
}
```

**Priority**: 🟡 Medium (Next sprint)

---

### 11. No Explicit Late Event Arrival Tests 🟡

**Severity**: Medium  
**Impact**: Late event handling not explicitly verified

**Problem**:
Test suites don't include explicit scenarios for late event arrivals (e.g., `PaymentSucceeded` arriving after order cancellation).

**Current State**:
- Timeout handling tests exist (e.g., `PaymentUnknown` state)
- Compensation tests exist (e.g., refund after cancellation)
- But no tests for: "What if `InventoryReserved` arrives 5 minutes late?"

**Recommended Test Cases**:
```csharp
[Fact]
public async Task Handle_LateInventoryReserved_AfterOrderCancelled_DoesNotResurrect()
{
    // Arrange
    var orderId = Guid.NewGuid();
    
    // Create order
    await SendCommand(new CreateOrderCommand(orderId, ...));
    
    // Cancel order due to timeout
    await SendCommand(new CancelOrderCommand(orderId, CancellationReason.Timeout));
    
    // Late event arrives
    var lateEvent = new InventoryReserved
    {
        OrderId = orderId,
        ReservationId = Guid.NewGuid(),
        OccurredAtUtc = DateTime.UtcNow.AddMinutes(-5)  // Old timestamp
    };
    
    // Act
    await consumer.Consume(lateEvent);
    
    // Assert
    var order = await _dbContext.Orders.FindAsync(orderId);
    Assert.Equal(OrderStatus.Cancelled, order.Status);  // Still cancelled
    
    // Verify compensation triggered
    var releaseCommand = await _dbContext.OutboxMessages
        .Where(m => m.Type == nameof(ReleaseInventory))
        .SingleOrDefaultAsync();
    Assert.NotNull(releaseCommand);
}
```

**Priority**: 🟡 Medium (Next sprint)

---

## Low Priority Observations

### 12. MessageEnvelope Fallback Logic 🟢

**Severity**: Low (Observation, correctly implemented)  
**File**: `src/BuildingBlocks/Messaging/Contracts/MessageEnvelope.cs:27-28`

**Code**:
```csharp
CorrelationId = correlationId.HasValue && correlationId.Value != Guid.Empty 
    ? correlationId.Value 
    : Guid.NewGuid();
```

**Analysis**:
This is **correct** — fallback to new GUID ensures every message has a correlation ID for workflow tracing, even when caller forgets to pass one.

**No action required** ✅

---

## Positive Findings ✅

The following architectural patterns are correctly implemented:

1. ✅ **Payment timeout handling**: Correctly models `PaymentUnknown` state instead of treating timeout as failure
2. ✅ **Webhook HMAC validation**: Prevents tampering with webhook payloads
3. ✅ **Inventory expiry checks**: Happen inside transactions (not just background job deleting cache)
4. ✅ **Frontend exhaustive state rendering**: TypeScript strict mode + union types force handling all order states
5. ✅ **Service boundaries**: No cross-service database queries observed
6. ✅ **Message envelope**: CorrelationId, CausationId, ContractVersion correctly implemented
7. ✅ **EF Core concurrency tokens**: Payment and Inventory aggregates have `RowVersion` properties
8. ✅ **Idempotency key storage**: Payment Service stores operation reference for query-before-retry pattern
9. ✅ **Message deduplication**: MassTransit InMemoryOutboxMessageRepository used correctly

---

## Recommended Fix Order

### Sprint 1 (Immediate)
1. **Day 1-2**: Fix Inventory outbox violations (#1)
   - Move `Publish()` calls after `SaveChangesAsync()` in all 4 consumers
   - Add integration test: "rollback does not publish phantom event"
   
2. **Day 3-5**: Migrate Cart to SQL (#2, #3, #4)
   - Add `CartDbContext`, EF Core configuration
   - Add `RowVersion` to Cart aggregate
   - Implement `EfCartRepository`
   - Configure MassTransit outbox
   - Publish domain events
   - Keep Redis for read-model caching
   - Add concurrency exception handling

3. **Day 6**: Connect frontend to API (#5)
   - Generate TypeScript client from Catalog OpenAPI
   - Replace hardcoded `sampleProducts` with API call
   - Add loading/error states

4. **Day 7**: Payment MessageId idempotency (#6)
   - Store `MessageId` as idempotency key
   - Check both `OrderId` and `MessageId` in deduplication logic

### Sprint 2
- Fix Payment reconciliation retry (#7)
- Add Catalog concurrency handling (#8)
- Generate all OpenAPI clients (#9)

### Sprint 3
- Add Testcontainers to integration tests (#10)
- Write late event arrival tests (#11)

---

## Appendix: Architecture Compliance Matrix

| Rule | Status | Notes |
|------|--------|-------|
| Service boundaries | ✅ Pass | No cross-service DB queries |
| Transactional outbox | ❌ Fail | Inventory publishes before commit |
| Idempotency | ⚠️ Partial | Payment checks OrderId but not MessageId |
| Concurrency control | ⚠️ Partial | Payment/Inventory have RowVersion, Cart missing |
| Timeout = Unknown | ✅ Pass | Payment models `PaymentUnknown` state |
| Late event handling | ⚠️ Untested | Logic exists but not explicitly tested |
| Redis = Cache only | ❌ Fail | Cart uses Redis as authoritative storage |
| Message envelope | ✅ Pass | CorrelationId, CausationId, ContractVersion correct |
| Frontend → Gateway only | ⚠️ Partial | Uses gateway but calls hardcoded data |
| OpenAPI client generation | ❌ Fail | No generated clients |

---

**Report Generated**: 2026-10-16  
**Reviewer**: Claude Code Agent  
**Next Review**: After Sprint 1 fixes (2026-11-01)

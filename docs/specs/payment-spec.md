# Payment Service Specification

## 1. Overview & Service Boundaries
The Payment Service manages payment processing attempts, mock external provider interactions, timeout handling (`PaymentUnknown`), provider transaction reconciliation, refunds, and authenticated webhook callbacks.

- **Boundary**: Sole authority for payment processing and payment attempt status. Payment status is **NEVER** stored in Redis.
- **Source of Truth**: SQL Server (`ShopSphere.Payment` database, schema `payment`) with EF Core optimistic concurrency tokens (`RowVersion`).
- **Transactional Outbox**: All state mutations and outgoing integration events (`PaymentSucceeded`, `PaymentRejected`, `PaymentPendingResolution`) are committed in the same SQL transaction via MassTransit's transactional outbox.
- **Resilience & Timeout Handling**: **Timeout = Unknown, not Failure**. A timeout or transient network failure when contacting the external payment gateway marks the state as `PaymentUnknown`. The system never blindly retries a charge operation; instead, it queries the provider via background reconciliation or awaits a webhook callback.
- **Late Event Protection**: Concurrency checks and current state verification prevent late events from corrupting aggregate state.

```
+-----------------------------------------------------------------------------------+
|                                  Order / Saga                                     |
+-----------------------------------------------------------------------------------+
       | CollectPayment
       v
+-----------------------------------------------------------------------------------+
|                       Payment Service (RabbitMQ Consumer)                         |
+-----------------------------------------------------------------------------------+
       |                                       |
       | Call ChargeAsync                      | Mark PaymentUnknown (on timeout)
       v                                       v
+-----------------------------+         +-------------------------------------------+
| External / Mock Provider   |         | SQL Server (payment.PaymentAttempts)      |
| (Polly resilience / timeout)|         | Concurrency Token (RowVersion) + Outbox   |
+-----------------------------+         +-------------------------------------------+
       |                                       ^
       | Webhook / Reconcile                   |
       +---------------------------------------+
       |
       | PaymentSucceeded / PaymentRejected / PaymentPendingResolution
       v
+-----------------------------------------------------------------------------------+
|                                 RabbitMQ Broker                                   |
+-----------------------------------------------------------------------------------+
```

---

## 2. Domain Model & Invariants (`Payment.Domain`)

### 2.1 PaymentStatus Enum
- `Pending` (0): Initial payment attempt created, charge initiated.
- `PaymentUnknown` (1): Gateway call timed out or returned an ambiguous state. Awaiting reconciliation.
- `Succeeded` (2): Payment confirmed by provider. Terminal charge state.
- `Failed` (3): Payment declined or rejected by provider. Terminal charge state.
- `Refunded` (4): Payment successfully refunded after prior success.

### 2.2 PaymentAttempt Aggregate Root
- `Id` (`Guid`): Primary key (`PaymentAttemptId`).
- `OrderId` (`Guid`): Order business identifier.
- `Amount` (`decimal`): Charge amount (`> 0`).
- `Currency` (`string`): 3-character ISO currency code (e.g. `USD`, `VND`).
- `PaymentMethod` (`string`): Payment method identifier (e.g., `credit_card`, `mock`).
- `Status` (`PaymentStatus`): Current lifecycle state.
- `ProviderTransactionId` (`string?`): Gateway provider transaction identifier.
- `FailureReason` (`string?`): Failure or rejection message from provider.
- `RowVersion` (`byte[]`): SQL Server timestamp/rowversion concurrency token.
- `CreatedAtUtc` (`DateTime`): Timestamp when attempt was created.
- `LastModifiedUtc` (`DateTime`): Timestamp of last transition.

#### Invariants & Methods
- `Create(Guid id, Guid orderId, decimal amount, string currency, string paymentMethod)`:
  - Validates `amount > 0`.
  - Validates `currency` (non-empty, uppercase, 3 chars).
  - Initializes `Status = PaymentStatus.Pending`.
- `MarkUnknown(string reason)`:
  - Valid only from `Pending` or `PaymentUnknown`.
  - Rejects if aggregate is in terminal state (`Succeeded`, `Failed`, `Refunded`) with `InvalidPaymentStateTransitionException`.
  - Updates `Status = PaymentStatus.PaymentUnknown`, `FailureReason = reason`, `LastModifiedUtc = DateTime.UtcNow`.
- `MarkSucceeded(string providerTransactionId)`:
  - Valid from `Pending` or `PaymentUnknown`.
  - Idempotent if already `Succeeded`.
  - Rejects if `Failed` or `Refunded`.
  - Updates `Status = PaymentStatus.Succeeded`, `ProviderTransactionId = providerTransactionId`, `FailureReason = null`, `LastModifiedUtc = DateTime.UtcNow`.
- `MarkFailed(string failureReason, string? providerTransactionId = null)`:
  - Valid from `Pending` or `PaymentUnknown`.
  - Idempotent if already `Failed`.
  - Rejects if `Succeeded` or `Refunded`.
  - Updates `Status = PaymentStatus.Failed`, `FailureReason = failureReason`, `ProviderTransactionId = providerTransactionId ?? ProviderTransactionId`, `LastModifiedUtc = DateTime.UtcNow`.
- `MarkRefunded(string refundReason)`:
  - Valid only from `Succeeded`.
  - Rejects from `Pending`, `PaymentUnknown`, `Failed`, or `Refunded`.
  - Updates `Status = PaymentStatus.Refunded`, `FailureReason = refundReason`, `LastModifiedUtc = DateTime.UtcNow`.

### 2.3 Domain Exceptions
- `InvalidPaymentStateTransitionException(PaymentStatus current, PaymentStatus target, string message)`
- `PaymentDomainException(string message)`

---

## 3. Persistence & Concurrency (`Payment.Infrastructure`)

### 3.1 Entity Framework Core Configuration
- Database context: `PaymentDbContext` in schema `payment`.
- Tables:
  - `payment.PaymentAttempts`:
    - `Id` (`Guid`, PK)
    - `OrderId` (`Guid`, Indexed, unique per attempt or indexed for order lookups)
    - `Amount` (`decimal(18,2)`)
    - `Currency` (`nvarchar(3)`)
    - `PaymentMethod` (`nvarchar(50)`)
    - `Status` (`nvarchar(30)`)
    - `ProviderTransactionId` (`nvarchar(100)`, nullable)
    - `FailureReason` (`nvarchar(500)`, nullable)
    - `CreatedAtUtc` (`datetime2`)
    - `LastModifiedUtc` (`datetime2`)
    - `RowVersion` (`rowversion`, concurrency token)
  - `payment.PaymentWebhooks`:
    - `Id` (`Guid`, PK)
    - `EventId` (`string`, unique index)
    - `EventType` (`string`)
    - `Payload` (`nvarchar(max)`)
    - `ProcessedAtUtc` (`datetime2`)
  - MassTransit Outbox tables (`InboxState`, `OutboxMessage`, `OutboxState`) in schema `payment`.

---

## 4. Provider Client & Timeout Resilience (`Payment.Infrastructure`)

### 4.1 Interface `IPaymentProviderClient`
```csharp
public record PaymentChargeRequest(Guid PaymentAttemptId, Guid OrderId, decimal Amount, string Currency, string PaymentMethod);
public record PaymentChargeResult(PaymentProviderStatus Status, string? ProviderTransactionId, string? ErrorMessage);

public record PaymentStatusQueryRequest(Guid PaymentAttemptId, string? ProviderTransactionId);
public record PaymentStatusQueryResult(PaymentProviderStatus Status, string? ProviderTransactionId, string? ErrorMessage);

public enum PaymentProviderStatus
{
    Success,
    Failed,
    Unknown
}
```

### 4.2 Resilience Strategy
- Charge operations use HTTP timeout and Polly resilience pipeline (`Microsoft.Extensions.Http.Resilience`).
- On `TimeoutException`, `TaskCanceledException`, or HTTP 5xx:
  - **NEVER** treat as `Failed`.
  - Return `PaymentProviderStatus.Unknown`.
  - Never automatically re-charge on unknown timeout.

---

## 5. Messaging & Consumers (`Payment.Application`)

### 5.1 Message Contracts
- **Command**: `CollectPayment(Guid OrderId, decimal Amount, string Currency, string PaymentMethod)`
- **Event**: `PaymentSucceeded(Guid OrderId, Guid PaymentAttemptId, string ProviderTransactionId, decimal Amount, string Currency)`
- **Event**: `PaymentRejected(Guid OrderId, Guid PaymentAttemptId, string Reason)`
- **Event**: `PaymentPendingResolution(Guid OrderId, Guid PaymentAttemptId, string Reason)`

### 5.2 CollectPaymentConsumer
1. Check if `PaymentAttempt` for `OrderId` exists:
   - If already `Succeeded`: re-publish `PaymentSucceeded` or return (idempotent).
   - If already `Failed`: re-publish `PaymentRejected` or return.
   - If already `PaymentUnknown`: log and do not initiate duplicate charge.
2. If none exists, create `PaymentAttempt` in `Pending` state and save to DB.
3. Call `IPaymentProviderClient.ChargeAsync`.
4. Update aggregate status:
   - If `Success`: `MarkSucceeded(txId)`, publish `PaymentSucceeded` via Outbox.
   - If `Failed`: `MarkFailed(reason)`, publish `PaymentRejected` via Outbox.
   - If `Unknown`: `MarkUnknown(reason)`, publish `PaymentPendingResolution` via Outbox.
5. Save changes inside the transaction.

---

## 6. Background Workers & Reconciliation (`Payment.Worker`)

### 6.1 PaymentReconciliationWorker
- BackgroundService scheduled to run periodically (e.g. every 15-30 seconds).
- Queries `payment.PaymentAttempts` where `Status == PaymentStatus.PaymentUnknown`.
- For each attempt:
  - Calls `IPaymentProviderClient.GetStatusAsync(...)`.
  - If provider confirms `Success`: transitions to `Succeeded`, saves, publishes `PaymentSucceeded` via Outbox.
  - If provider confirms `Failed`: transitions to `Failed`, saves, publishes `PaymentRejected` via Outbox.
  - If still `Unknown`: leaves attempt for subsequent sweep.

---

## 7. Webhook & REST API (`Payment.Api`)

### 7.1 Webhook Endpoint
- `POST /api/v1/payment/webhooks/mock-provider`
  - Validates HMAC-SHA256 signature in `X-Payment-Signature` header against secret key.
  - Idempotency check against `payment.PaymentWebhooks` via `EventId`.
  - Finds corresponding `PaymentAttempt`.
  - If attempt is `PaymentUnknown` or `Pending`:
    - Transitions to `Succeeded` or `Failed`.
    - Emits `PaymentSucceeded` or `PaymentRejected` via Outbox.
  - Returns `200 OK`.

### 7.2 API Endpoints & Health
- `GET /api/v1/payment/attempts/{id}`: Returns payment attempt details.
- `GET /health`: Checks SQL Server and RabbitMQ connections.
- OpenAPI 3.0 specification in `src/Services/Payment/Payment.Api/openapi.json`.
- Postman collection in `docs/postman/Payment.postman_collection.json`.

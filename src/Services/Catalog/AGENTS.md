# Catalog Service Ownership

> **For all AI agents**: This file defines ownership, contracts, and failure rules for the Catalog service. Read before working on this service.

## Ownership

| Resource | Owner | Access Pattern |
|----------|-------|----------------|
| `catalog` database schema / `Products`, `OutboxMessages` | Catalog service | Exclusive write access |
| `Catalog.ProductPriceChanged` events | Catalog service | Publisher |
| Catalog & pricing business rules | Catalog service | Authority |

**External dependencies**:
- Redis: Read-cache for catalog queries (invalidated on price/product updates)
- RabbitMQ: Outbox message broker delivery

## Message Contracts

### Published Events

**Catalog.Events.ProductPriceChanged**
```csharp
public record ProductPriceChanged(
    Guid ProductId,
    string Sku,
    decimal OldPrice,
    decimal NewPrice,
    string Currency,
    int NewPriceVersion
)
{
    public Guid MessageId { get; init; }
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public int ContractVersion { get; init; } = 1;
}
```

**When**: Product price is updated and committed.
**Consumers**: CartService (re-evaluate active carts), OrderService (audit).
**Idempotency**: Handled via `MessageId` and `NewPriceVersion`.

## State Machine

```
[Draft] --[Activate]--> [Active] --[Discontinue]--> [Discontinued]
```

**Rules**:
- Discontinued products cannot be edited or reactivated.
- Discontinuing a product does not erase history in old orders.
- Price updates increment `PriceVersion`.

## Concurrency Control

- **Strategy**: Optimistic concurrency via SQL Server `rowversion` (`byte[] RowVersion`).
- **Conflict Handling**: Handle `DbUpdateConcurrencyException` explicitly; return HTTP 409 Conflict with descriptive message.
- **Never**: Silently overwrite concurrent edits without checking `RowVersion`.

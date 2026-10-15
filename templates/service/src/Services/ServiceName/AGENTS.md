# ServiceName Service Ownership

> **For all AI agents**: This file defines ownership, contracts, and failure rules for the ServiceName service. Read before working on this service.

## Ownership

| Resource | Owner | Access Pattern |
|----------|-------|----------------|
| `ServiceName` database/schema | ServiceName service | Exclusive write access |
| `ServiceName.*` messages | ServiceName service | Publisher |
| ServiceName business rules | ServiceName service | Authority |

**External dependencies**:
- [Dependency service]: [What data/operation] via [HTTP API or Message subscription]

## Message Contracts

### Published Events

**ServiceName.Events.{EventName}**
```csharp
public record EventName(
    string ExampleId,
    string Property
)
{
    public Guid MessageId { get; init; }
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }
    public DateTime OccurredAtUtc { get; init; }
    public int ContractVersion { get; init; } = 1;
}
```

**When**: [Business condition that triggers this event]  
**Consumers**: [Which services subscribe]  
**Idempotency**: MessageId

### Consumed Events

**OtherService.Events.EventName**
- **Why**: [Business reason for subscribing]
- **Action**: [What this service does in response]
- **Failure handling**: [Retry policy, dead letter queue strategy]

## State Machine

```
[State1] --[Trigger]--> [State2]
  |
  +--[FailureTrigger]--> [FailureState]
```

**States**:
- State1: [Description]
- State2: [Description]
- FailureState: [Description, compensating action]

## Concurrency Control

- Uses EF Core `rowversion` tokens on [Entity names]
- `DbUpdateConcurrencyException` → [Strategy: retry with fresh state / reject / escalate]

## Timeout Handling

**External service calls**:
- [Service name API]: Timeout [Xms] → State becomes `[EntityState]Unknown`
  - Store [AttemptId/RequestId] with request
  - Query [service status endpoint] before retry
  - States: `Pending`, `[Operation]Unknown`, `Confirmed`, `Failed`

## Late Events

**Scenario**: [EventName] arrives after [related state transition]

**Check**: Current state/version before applying event  
**If late**: [Compensation strategy or escalation]

## Expiry

**Reservation/Hold expiry**:
- Checked inside transaction that commits/releases resource
- Not just background job deleting cache entry
- Expiry transitions entity to [ExpiredState]

## Redis Cache

**Cache**: [Read model names, catalog data]  
**Never cache**: [Inventory counts, reservation status, payment state]

## Idempotency

**Operation deduplication**:
- Key: [Business identifier, e.g., OrderId + UserId]
- Stored: Request fingerprint, operation code, result
- Reject: Same key + different content

**Message deduplication**:
- Key: MessageId
- Consumer tracks processed MessageIds in DB

## API Endpoints

All endpoints documented in OpenAPI spec: `ServiceName.Api/openapi.json`

Postman collection: [Workspace: ShopSphere, Collection: ServiceName]

**Critical endpoints**:
- `POST /api/v1/resource`: [Operation description]
- `GET /api/v1/resource/{id}`: [Operation description]

## Test Requirements

Every workflow test suite includes:
- ✅ Happy path
- ✅ Out of stock / resource unavailable
- ✅ External service timeout (state becomes Unknown)
- ✅ External service rejected
- ✅ Late event after state transition
- ✅ Concurrent update conflict
- ✅ Expiry edge case

## Common Pitfalls

❌ **Don't**: Access [other service] database directly  
✅ **Do**: Call [other service] HTTP API or subscribe to its events

❌ **Don't**: Retry [operation] blindly on timeout  
✅ **Do**: Store attempt ID, keep state as Unknown, query status before retry

❌ **Don't**: Assume cache has latest [resource] state  
✅ **Do**: [Operation] via transactional API, cache reads only

❌ **Don't**: Publish message without Outbox  
✅ **Do**: Write data + message in same transaction

---

**Questions?** Check root AGENTS.md or ask in PR for clarification.

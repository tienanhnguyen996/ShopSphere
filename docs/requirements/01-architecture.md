# Architecture and Shared Technical Foundation

## Service Boundaries and Communication

Each service owns its business capability, its data, and its rules. Microservices communicate through synchronous APIs (HTTP/gRPC) or an asynchronous messaging system (Command/Event), choosing Command or Event according to the business purpose. **A service must not access another microservice's internal data tables directly.**

For local development, services may share one SQL Server instance, but they must separate the database or schema, the accounts, and the migrations per service. The data ownership principle is described by Microsoft in [Data sovereignty per microservice](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/architect-microservice-container-applications/data-sovereignty-per-microservice).

### Communication Patterns

**Synchronous (HTTP/gRPC)**: Lookups that must return immediately, permission checks, getting a quote, or submitting a request and receiving an acknowledgment code. Avoid long call chains on the user's processing path.

**Asynchronous (messaging)**: Long-running business orchestration, updating projections, sending notifications. The API may return a business code and a "processing" status, and the client looks up the result later.

**Command**: Asks a service to perform an action, for example `ReserveInventory`.

**Event**: Announces something that has happened, for example `InventoryReserved`. Multiple services can subscribe to it.

> **Note**: The message names in this document are contracts defined by the project, not built-in APIs or messages of MassTransit.

## Message Envelope

Proposed envelope: `MessageId`, `CorrelationId`, `CausationId`, `OccurredAtUtc`, `ContractVersion`, and a business code. Add `TenantId` only when deploying multiple tenants. Propagate trace context across HTTP, the broker, and workers to link the processing steps together.

## Consistency and Error Handling

### Eventual Consistency

Services may update at different times, so the API must be able to expose intermediate states.

### Idempotency

Resending the same operation must not create additional business effects.

### Consistency Mechanisms

| Mechanism | Role and Limits |
|-----------|-----------------|
| **Transactional Outbox** | Writes business data and the pending message in the same local transaction. A relay delivers the message to the broker after commit. It does not create a transaction across services. |
| **MassTransit Bus Outbox** | Suitable when the API/application scope both writes to the DB and sends/publishes. |
| **Consumer Outbox / Inbox pattern** | Coordinates the processing state of incoming messages, business data, and outgoing messages. A business lock against duplicates is still needed for external side effects. |
| **RabbitMQ** | The broker transports messages. Publisher confirms and consumer acknowledgements cover different hops. Do not infer that a business transaction is complete from a broker confirm. |
| **Saga Orchestration** | A coordinator stores state, sends commands, handles results and timeouts. |
| **Saga Choreography** | Participants react to events and perform the next step themselves. There is no central coordinator commanding the whole workflow. |
| **Compensation** | A compensating action, for example returning stock, releasing a slot, or requesting a refund. It is a new business transaction and can fail. |
| **Reconciliation** | Re-checks the result when the provider may have executed the operation but the response was lost. Record the conclusion or hand it off to manual handling. |

**References**: 
- Outbox/Inbox → [MassTransit Outbox](https://masstransit.io/documentation/patterns/outbox) and [Outbox Configuration](https://masstransit.io/documentation/configuration/middleware/outbox)
- Confirm vs. ack → [RabbitMQ acknowledgements](https://www.rabbitmq.com/confirms.html)
- The two Saga approaches and the limits of compensation → [Azure Saga pattern](https://learn.microsoft.com/en-us/azure/architecture/reference-architectures/saga/saga)

## Implementation Rules for Workflows

1. Store the idempotency key together with the request fingerprint, the operation code, and the result in the DB. The same key with different content must be rejected.

2. Process messages on the assumption they can be redelivered. Use `MessageId` for deduplication and a business operation ID to prevent duplicated effects.

3. Every resource reservation has an owner, a status, and an expiry. Expiry is a conditional business state transition, not just deleting a cache entry.

4. Protect concurrent updates with constraints and concurrency control. EF Core supports concurrency tokens, and SQL Server rowversion is one option, but the application must still handle conflicts (see [EF Core concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)).

5. Retry transient errors with a limit. Business rejections follow the failure branch. Persistently failing messages need an error queue and a controlled replay process.

6. **A timeout on an external transaction is an unknown result, not automatically a failure.** Store an operation reference so you can inquire before creating a new operation.

7. A late event must not resurrect a cancelled transaction. Check state/version. If a side effect has already happened, open compensation or reconciliation.

8. A failed notification does not reverse completed business. Notifications have their own state and retry.

## Role of the Platform Stack

| Component | Proposed Application |
|-----------|---------------------|
| **C# / .NET** | Domain model, application handlers, and background workers. Choose a compatible set of versions and lock dependencies at deployment. |
| **ASP.NET Core** | Each service's API, validation, authentication/authorization, and health endpoints. |
| **EF Core / SQL Server** | Persistence, local transactions, migrations, constraints, Saga state, and Outbox, depending on the service. |
| **MassTransit / RabbitMQ** | Consumers, send/publish, correlation, retry/redelivery, and Saga where needed. RabbitMQ handles transport. |
| **Redis** | Cache for read data with TTL, invalidation, and per-service namespaces. **It is not by default the source of truth for balances, available slots, or access rights.** |
| **OpenTelemetry** | Collects logs, metrics, and distributed traces. Exports to a separately chosen observability backend. |
| **Docker** | Packages services. Docker Compose runs the API, workers, database, broker, and cache locally. |
| **Terraform** | Infrastructure as Code for cloud environments. Manages resources, providers, and state per environment. |

### Notes

- Redis can be integrated through `IDistributedCache` and `Microsoft.Extensions.Caching.StackExchangeRedis` (see [Distributed caching](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed)).
- OpenTelemetry provides the mechanism for collecting and exporting telemetry. It does not provide a dashboard by itself (see [OpenTelemetry in .NET](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel)).
- Deployment tool: Terraform.
- OAuth 2.0, Saga, CQRS, Outbox, Inbox, Idempotency, and Docker Compose are protocols or design patterns. Not all of them are packages.
- Topic-specific stacks are analyzed within each project topic.

## MassTransit Version

The official documentation now states that MassTransit v9 is a commercial product. When implementing, record the major version, the licensing terms, and the matching documentation. Do not assume that all versions share the same usage terms.

**Decision for this project**: See ADR 0001 in docs/adr/

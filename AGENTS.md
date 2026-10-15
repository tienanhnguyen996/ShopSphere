# ShopSphere Agent Instructions

> **For all AI agents**: These are the architectural and workflow rules for the ShopSphere e-commerce monorepo. Read this before writing any service code.

## Architecture Rules

**Service boundaries**: Each service owns its data, database/schema, and business rules. Never access another service's tables directly. Use HTTP for immediate lookups, messaging for workflows.

**Message envelope**: Every message includes `MessageId` (deduplication), `CorrelationId` (workflow linking), `CausationId` (what triggered this), `OccurredAtUtc`, `ContractVersion`, and a business identifier.

**Transactional Outbox**: Write business data and pending messages in the same transaction. A background relay delivers to RabbitMQ after commit. This prevents lost/duplicate messages.

**Idempotency**: Store the idempotency key with request fingerprint, operation code, and result. Reject same key + different content. Use `MessageId` for message deduplication, business ID for operation deduplication.

**Concurrency control**: Use EF Core concurrency tokens (SQL Server `rowversion`). Handle `DbUpdateConcurrencyException` explicitly — retry, reject, or escalate. Never silently ignore.

**Timeout = Unknown, not Failure**: A timeout calling an external service (payment provider, shipping API) means **unknown result**, not automatic failure. Store an operation reference (PaymentAttemptId, ShipmentRequestId). Query the provider before retrying. Model explicit states: `PaymentUnknown`, `ShipmentUnknown`.

**Late events**: Check current state/version before applying an event. A late `PaymentSucceeded` must not resurrect a cancelled order. If side effects already happened, open compensation or reconciliation.

**Redis is cache, not source of truth**: Cache catalog, cart read models. Never cache: inventory counts, payment state, reservation status, access rights. These live in SQL with transactions and concurrency control.

**Expiry is a state transition**: Reservation expiry checks happen inside the transaction that commits/releases stock, not just a background job deleting a cache entry.

**API documentation**: When adding or modifying an API endpoint, document it in the service's OpenAPI spec AND add it to the Postman collection via the Postman MCP server. Every endpoint must have a working Postman request with example payloads.

## Frontend Rules

**Gateway only**: Frontend calls the API gateway/BFF, never individual services directly. YARP gateway routes to services.

**Generated OpenAPI client**: TypeScript client generated from service OpenAPI specs. No hand-written fetch code.

**Strict TypeScript**: `strict: true`, no `any`, exhaustive switch on union types.

**No cross-feature imports**: Cart feature cannot import from Order feature. Shared UI primitives live in a dedicated shared/ folder.

**Render all states**: Order states include Pending, Reserving, PaymentUnknown, Paying, Confirmed, Cancelled, Refunding. Don't assume success/failure binary.

## Workflow Rules

**Spec first**: Write service spec in `docs/specs/` before implementation. Include message contracts, state transitions, failure paths.

**One service per task**: Focus on one service at a time. Complete its domain, API, tests before moving to the next.

**Test failure paths**: Every saga/workflow test suite includes: out of stock, payment rejected, payment timeout, late event, concurrent update conflict, expiry edge case.

**Never `terraform apply`**: Only run `terraform plan` and `terraform validate`. Applying infrastructure changes requires explicit approval.

**No new packages without ADR**: Adding a new NuGet package or npm dependency requires an ADR in `docs/adr/`. Explain why existing options don't fit.

**Ask before destructive actions**: Before `git push --force`, `git reset --hard`, `rm -rf`, `DROP TABLE`, ask for confirmation. These are already denied in `.claude/settings.json` but check regardless.

## Development Process

1. **Read requirements**: Check `docs/requirements/` for architectural patterns
2. **Check ADRs**: Review decisions in `docs/adr/` for precedent
3. **Write spec** (if new feature): API, messages, state machine, failure handling
4. **Implement**: Follow existing patterns, respect service boundaries
5. **Test**: Unit + integration (Testcontainers for DB/broker), failure paths
6. **Run doctor**: `bash scripts/doctor.sh` before committing
7. **Commit**: Follow conventional commits, reference issue/story if applicable
8. **PR**: Use template checklist, include test plan

## Key References

- **Architecture**: [docs/requirements/01-architecture.md](./requirements/01-architecture.md)
- **Services**: [docs/requirements/02-services.md](./requirements/02-services.md)
- **Messaging**: [docs/requirements/03-messaging.md](./requirements/03-messaging.md)
- **Infrastructure**: [docs/requirements/04-infrastructure.md](./requirements/04-infrastructure.md)
- **Agent Setup**: [docs/agent-setup.md](./agent-setup.md) — MCP configuration for GitHub, Notion, Terraform tools

## Common Pitfalls

❌ **Don't**: Query another service's database  
✅ **Do**: Call its HTTP API or subscribe to its events

❌ **Don't**: Retry payment charge blindly on timeout  
✅ **Do**: Store attempt ID, keep state as `PaymentUnknown`, query status before retry

❌ **Don't**: Assume Redis has the latest inventory count  
✅ **Do**: Reserve stock via InventoryService transaction, cache reads only

❌ **Don't**: Publish a message without Outbox  
✅ **Do**: Write business data + message in same transaction, rely on Outbox relay

❌ **Don't**: Ignore `DbUpdateConcurrencyException`  
✅ **Do**: Handle explicitly (retry with fresh state or reject)

❌ **Don't**: Let a late event resurrect a cancelled workflow  
✅ **Do**: Check current state/version, compensate if side effects happened

## Health Checks

Before every commit, run:
```bash
bash scripts/doctor.sh
```

This verifies:
- Tool versions (SDK, Node, Docker, Terraform)
- Docker running
- `.env` has required tokens
- Compose infra healthy
- MCP config valid
- Skills present

If doctor fails, fix before committing.

---

**Questions?** Check `docs/requirements/` or ask in PR for clarification. Never guess architectural patterns — follow established rules.

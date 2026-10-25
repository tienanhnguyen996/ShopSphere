# Technical Debt Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remediate critical and high-priority technical debt identified in `docs/code-review-2026-10-16.md`, focusing on transactional outbox ordering, concurrency conflict handling, payment message idempotency, and Notion roadmap status alignment.

**Architecture:** Enforce the transactional outbox pattern across all Inventory and Payment workers/consumers by ensuring database state commits occur prior to external dispatch; add explicit `DbUpdateConcurrencyException` handling with retry and domain conflict errors in Catalog and Payment services; update Notion tasks to reflect merged identity/auth deliverables.

**Tech Stack:** .NET 10, EF Core 10, SQL Server, MassTransit 8, Redis, xUnit, FluentAssertions, Moq.

**Spec:** `docs/code-review-2026-10-16.md`

## Global Constraints

- Never commit code that breaks existing unit tests (`dotnet test`).
- Preserve all existing comments and public API contracts.
- Follow Conventional Commits: `<type>(<scope>): [Task-XX] <description>`.
- In all consumers and workers, ensure `_dbContext.SaveChangesAsync()` commits entity modifications before or atomically with outbox message dispatch.
- Concurrency conflicts must never result in an unhandled 500 error; return `Error.Conflict` or retry with exponential backoff.

## Review Focus

1. Concurrency exception during price update returns HTTP 409 Conflict with `Error.Conflict` instead of unhandled 500.
2. Inventory stock reservation rollback on DB failure never emits phantom `InventoryReserved` event.
3. Payment reconciliation worker reloads fresh state and retries on `DbUpdateConcurrencyException`.
4. Payment consumer handles duplicate `CollectPayment` commands without double charging.
5. All 404+ backend unit tests continue passing without regression.

---

### Task 1: Fix Inventory Transactional Outbox Dispatch Sequence (#1)

**Files:**
- Modify: `src/Services/Inventory/Inventory.Application/Consumers/ReserveInventoryConsumer.cs:134-163`
- Modify: `src/Services/Inventory/Inventory.Application/Workers/ReservationExpiryWorker.cs:118-127`
- Test: `tests/Inventory.Tests.Unit/Consumers/ReserveInventoryConsumerTests.cs`
- Test: `tests/Inventory.Tests.Unit/Workers/ReservationExpiryWorkerTests.cs`

- [ ] **Step 1: Inspect ReserveInventoryConsumer test suite and verify current behavior**
Run: `dotnet test --filter "FullyQualifiedName~ReserveInventoryConsumerTests"`

- [ ] **Step 2: Update ReserveInventoryConsumer to save changes before publishing outbox envelopes**
In `ReserveInventoryConsumer.cs`:
- For successful holds: call `await _dbContext.SaveChangesAsync(ct)` BEFORE `publishEndpoint.Publish(successEnvelope, ct)` and `publishEndpoint.Publish(successEnvelope.Payload, ct)`.
- For insufficient stock rejection: call `await _dbContext.SaveChangesAsync(ct)` before or without unnecessary double-publish if no DB changes were staged.
- For concurrency retry exhaustion: call `await _dbContext.SaveChangesAsync(ct)` before `publishEndpoint.Publish(rejectedEnvelope, ct)`.

- [ ] **Step 3: Update ReservationExpiryWorker to save changes before publishing expiry events**
In `ReservationExpiryWorker.cs`:
- Call `await dbContext.SaveChangesAsync(ct)` immediately after `stockItem.ExpireHold(reservation.Id)`.
- Then call `await publishEndpoint.Publish(envelope, ct)` and `await publishEndpoint.Publish(envelope.Payload, ct)`.

- [ ] **Step 4: Run Inventory unit tests to verify zero regressions**
Run: `dotnet test --filter "FullyQualifiedName~Inventory.Tests.Unit"`

- [ ] **Step 5: Commit changes**
`git commit -m "fix(inventory): [Task-29] ensure database commit occurs before outbox message dispatch"`

---

### Task 2: Catalog Service Concurrency Exception Handling (#8)

**Files:**
- Modify: `src/Services/Catalog/Catalog.Application/Products/Commands/UpdatePrice/UpdateProductPriceCommandHandler.cs:88-92`
- Test: `tests/Catalog.Tests.Unit/Application/Products/Commands/UpdateProductPriceCommandHandlerTests.cs`

- [ ] **Step 1: Write a unit test simulating DbUpdateConcurrencyException during price update**
In `UpdateProductPriceCommandHandlerTests.cs`:
- Add test `HandleAsync_WhenDbUpdateConcurrencyExceptionThrown_ReturnsConflictError`.
- Verify it returns `Result.Failure<ProductDto>` with `ErrorType.Conflict` and error code `"Product.Conflict"`.

- [ ] **Step 2: Implement DbUpdateConcurrencyException handling in UpdateProductPriceCommandHandler**
In `UpdateProductPriceCommandHandler.cs`:
- Wrap `await _dbContext.SaveChangesAsync(ct)` in a `try...catch (DbUpdateConcurrencyException)` block.
- Return `Result.Failure<ProductDto>(Error.Conflict("Product.Conflict", "The product price was modified by another operation. Please refresh and try again."))`.

- [ ] **Step 3: Run Catalog unit tests**
Run: `dotnet test --filter "FullyQualifiedName~Catalog.Tests.Unit"`

- [ ] **Step 4: Commit changes**
`git commit -m "fix(catalog): [Task-15] handle DbUpdateConcurrencyException gracefully with conflict result"`

---

### Task 3: Payment Service Concurrency Retry & Outbox Ordering (#7)

**Files:**
- Modify: `src/Services/Payment/Payment.Application/Workers/PaymentReconciliationWorker.cs:130-155`
- Test: `tests/Payment.Tests.Unit/Workers/PaymentReconciliationWorkerTests.cs`

- [ ] **Step 1: Inspect PaymentReconciliationWorkerTests**
Run: `dotnet test --filter "FullyQualifiedName~PaymentReconciliationWorkerTests"`

- [ ] **Step 2: Implement retry loop with backoff for concurrency conflicts in PaymentReconciliationWorker**
In `PaymentReconciliationWorker.cs`:
- Move `await dbContext.SaveChangesAsync(ct)` before `publishEndpoint.Publish(...)`.
- Add retry loop (up to 3 retries) with exponential backoff (`Task.Delay(100 * (retry + 1), ct)`) when `DbUpdateConcurrencyException` occurs, reloading fresh state from `dbContext.PaymentAttempts.FindAsync()`.

- [ ] **Step 3: Run Payment unit tests**
Run: `dotnet test --filter "FullyQualifiedName~Payment.Tests.Unit"`

- [ ] **Step 4: Commit changes**
`git commit -m "fix(payment): [Task-35] retry reconciliation on concurrency conflicts and fix outbox ordering"`

---

### Task 4: Payment Service MessageId-Based Deduplication (#6)

**Files:**
- Modify: `src/Services/Payment/Payment.Application/Consumers/CollectPaymentConsumer.cs:46-88`
- Test: `tests/Payment.Tests.Unit/Consumers/CollectPaymentConsumerTests.cs`

- [ ] **Step 1: Write unit test for duplicate message idempotency in CollectPaymentConsumer**
In `CollectPaymentConsumerTests.cs`:
- Add test verifying that redelivered messages with identical or known attempt keys return existing attempt results without recharging.

- [ ] **Step 2: Update CollectPaymentConsumer to check idempotency key / message envelope ID**
In `CollectPaymentConsumer.cs`:
- Use `context.MessageId` if present or fallback to command idempotency key.
- Verify existing attempts match and handle duplicate events safely.

- [ ] **Step 3: Run Payment unit tests**
Run: `dotnet test --filter "FullyQualifiedName~Payment.Tests.Unit"`

- [ ] **Step 4: Commit changes**
`git commit -m "fix(payment): [Task-32] enforce message-level idempotency in CollectPaymentConsumer"`

---

### Task 5: Notion Roadmap Synchronization for Completed Tasks

**Files:**
- External Notion Database: `ccc732a6-4ebd-468e-a518-397ec2ee77f5` (Data source: `73a3aa6d-144b-47dd-b9c6-66fd3806ca4f`)

- [ ] **Step 1: Update Task 64 to Status = "Done"**
Page ID: `3fbe96c5-625d-81df-94ce-cc3071c81840`
PR URL: `https://github.com/tienanhnguyen996/ShopSphere/pull/10`

- [ ] **Step 2: Update Task 65 to Status = "Done"**
Page ID: `3fbe96c5-625d-8122-a259-d1ca3ed95d38`
PR URL: `https://github.com/tienanhnguyen996/ShopSphere/pull/10`

- [ ] **Step 3: Update Task 66 to Status = "Done"**
Page ID: `3fbe96c5-625d-81f0-9b5f-efe7b1150b54`
PR URL: `https://github.com/tienanhnguyen996/ShopSphere/pull/11`

- [ ] **Step 4: Verify all updated tasks in Notion show Done**

---

### Task 6: Full Solution Health & Regression Check

- [ ] **Step 1: Run all backend unit tests**
`dotnet test --filter "FullyQualifiedName!~Integration"`

- [ ] **Step 2: Run frontend tests & lint**
`pnpm --prefix src/Web test --run`
`pnpm --prefix src/Web lint`

- [ ] **Step 3: Run pre-commit doctor**
`bash scripts/doctor.sh` (or check version/git status)

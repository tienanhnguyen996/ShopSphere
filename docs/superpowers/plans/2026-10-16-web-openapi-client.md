# Web OpenAPI Client Code Generation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement automated TypeScript API client generation from backend OpenAPI specs (`Catalog`, `Cart`, `Inventory`, `Payment`) and configure the base client with automatic `X-Correlation-Id` header injection and Vite gateway proxy.

**Architecture:** `openapi-typescript-codegen` generates native fetch client modules under `src/Web/src/api/generated/` from `docs/specs/*-openapi.json`. A centralized wrapper in `src/Web/src/api/client.ts` sets `BASE = '/api'`, intercepts requests to inject `X-Correlation-Id`, and exports unified API instances.

**Tech Stack:** TypeScript 5.6, `openapi-typescript-codegen`, React 18, Vite 5, Node.js 20.

**Spec:** [`docs/specs/web-openapi-client-spec.md`](../../specs/web-openapi-client-spec.md) and [`docs/adr/0005-openapi-client-codegen.md`](../../adr/0005-openapi-client-codegen.md).

## Global Constraints

- Target TypeScript `strict: true`, no `any`, exhaustive types.
- Follow Conventional Commits with Notion Task ID: `<type>(web): [Task-11] <description>`.
- All code must pass style and formatting checks via `scripts/lint.ps1`.
- Frontend calls Gateway only (`/api`), never backend services directly.
- No hand-written `fetch` or `axios` calls allowed in feature code.

---

### Task 1: Codegen Tooling & Multi-Service Generation Script

**Files:**
- Modify: `src/Web/package.json`
- Create: `src/Web/scripts/generate-clients.mjs`
- Generate: `src/Web/src/api/generated/catalog/`
- Generate: `src/Web/src/api/generated/cart/`
- Generate: `src/Web/src/api/generated/inventory/`
- Generate: `src/Web/src/api/generated/payment/`

**Interfaces:**
- Produces: `pnpm run generate-client` script generating strongly typed services and DTO models for each microservice.

- [ ] **Step 1: Add `openapi-typescript-codegen` to `src/Web/package.json` devDependencies**
- [ ] **Step 2: Create generation runner `src/Web/scripts/generate-clients.mjs`**
  Iterates over `catalog`, `cart`, `inventory`, `payment` specs in `docs/specs/` and invokes `openapi-typescript-codegen` with `--client fetch`.
- [ ] **Step 3: Run `pnpm run generate-client` and verify output**
  Ensure all models and service classes are generated cleanly.
- [ ] **Step 4: Verify typecheck passes**
  Run `pnpm --dir src/Web run build` (`tsc && vite build`).
- [ ] **Step 5: Commit**
  ```bash
  git add src/Web/package.json pnpm-lock.yaml src/Web/scripts/ src/Web/src/api/generated/
  git commit -m "feat(web): [Task-11] add openapi client codegen script and generated clients"
  ```

---

### Task 2: Base Client Configuration, Correlation-ID Interceptor & Exports

**Files:**
- Create: `src/Web/src/api/client.ts`
- Create: `src/Web/src/api/index.ts`
- Create: `src/Web/src/api/client.test.ts`

**Interfaces:**
- Produces: Centralized API client configuring `BASE = '/api'`, automatic `X-Correlation-Id` header generation, token attachment, and re-export of all services.

- [ ] **Step 1: Write unit tests in `src/Web/src/api/client.test.ts`**
  Verify `getCorrelationId()` returns a valid UUID v4, and request interceptor attaches `X-Correlation-Id`.
- [ ] **Step 2: Implement `src/Web/src/api/client.ts` and `src/Web/src/api/index.ts`**
  Implement correlation ID generator/interceptor and configure `OpenAPI` settings for each generated service.
- [ ] **Step 3: Run unit tests and lint checks**
  Verify tests pass and `powershell -ExecutionPolicy Bypass -File scripts\lint.ps1` is clean.
- [ ] **Step 4: Commit**
  ```bash
  git add src/Web/src/api/
  git commit -m "feat(web): [Task-11] configure base api client with correlation id interceptor and exports"
  ```

---

### Task 3: CI Integration & Final Verification

**Files:**
- Modify: `.github/workflows/test.yml`

- [ ] **Step 1: Ensure frontend build & typecheck validates generated clients in CI**
  Verify `.github/workflows/test.yml` runs `pnpm --dir src/Web build`.
- [ ] **Step 2: Run full lint and verification**
  Run `powershell -ExecutionPolicy Bypass -File scripts\lint.ps1`.
- [ ] **Step 3: Commit**
  ```bash
  git add .github/workflows/test.yml
  git commit -m "ci(web): [Task-11] verify openapi client generation in CI workflow"
  ```

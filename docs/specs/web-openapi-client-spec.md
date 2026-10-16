# Web OpenAPI Client Code Generation & Gateway Proxy Specification

## 1. Overview

The ShopSphere web frontend (`src/Web`) communicates exclusively with backend services through the YARP API Gateway via Vite's development proxy (`/api -> http://localhost:5000`).

To ensure end-to-end type safety and prevent drift between frontend clients and backend contracts:
1. All client request methods and DTO types are automatically generated from backend OpenAPI 3.0 specifications.
2. Hand-written `fetch` or `axios` calls in feature components are strictly prohibited.
3. Every outbound HTTP request automatically includes an `X-Correlation-Id` header for distributed tracing.

---

## 2. Code Generation Pipeline

### 2.1 Tooling & Configuration
- **Package**: `openapi-typescript-codegen` (v0.29.0) added to `devDependencies` in `src/Web/package.json`.
- **NPM Script**:
  ```json
  "generate-client": "node scripts/generate-clients.mjs"
  ```

### 2.2 Input Specs & Target Directories
The generator script reads from `docs/specs/` and writes to namespaced folders in `src/Web/src/api/generated/`:

| Service | Spec Path | Target Output Directory |
|:---|:---|:---|
| **Catalog** | `docs/specs/catalog-openapi.json` | `src/Web/src/api/generated/catalog` |
| **Cart** | `docs/specs/cart-openapi.json` | `src/Web/src/api/generated/cart` |
| **Inventory** | `docs/specs/inventory-openapi.json` | `src/Web/src/api/generated/inventory` |
| **Payment** | `docs/specs/payment-openapi.json` | `src/Web/src/api/generated/payment` |

---

## 3. Base Client Configuration & Interceptors

### 3.1 Base URL & Vite Gateway Proxy
In development:
- Vite serves the frontend at `http://localhost:3000`.
- Vite proxies all `/api/*` requests to `http://localhost:5000` (YARP Gateway).
- Generated clients are configured with `BASE: '/api'`.

### 3.2 Correlation ID Injection
All generated clients must attach an `X-Correlation-Id` header to every outbound request:
- If a correlation ID context is already active, that ID is reused.
- If no correlation ID is present, a fresh UUID v4 is generated (`crypto.randomUUID()`).
- Header format: `X-Correlation-Id: <uuid>`.

### 3.3 Central API Client Entry Point
File: `src/Web/src/api/client.ts`
- Initializes `OpenAPI` configuration for each generated service.
- Configures default request interceptor for `X-Correlation-Id`.
- Provides helper functions for setting the authentication bearer token.

File: `src/Web/src/api/index.ts`
- Re-exports configured service clients: `catalogApi`, `cartApi`, `inventoryApi`, `paymentApi`.
- Re-exports all generated TypeScript models.

---

## 4. Verification & Testing

1. **Automated Generation**: Running `pnpm --dir src/Web run generate-client` creates pristine TypeScript files in `src/Web/src/api/generated/`.
2. **Typecheck & Build**: `pnpm --dir src/Web run build` (`tsc && vite build`) passes with zero TypeScript diagnostics.
3. **Lint & Code Style**: `pnpm --dir src/Web run lint` and `powershell -ExecutionPolicy Bypass -File scripts\lint.ps1` pass cleanly.
4. **Unit Tests**: Unit tests in `src/Web/src/api/client.test.ts` verify:
   - Base URL configuration defaults to empty string (`''`) since OpenAPI paths start with `/api`.
   - `X-Correlation-Id` header is injected on requests.
   - Client instances can be imported and initialized properly.
